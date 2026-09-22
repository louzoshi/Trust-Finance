using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using TrustFinance.Data.Mappings;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Notifications;

namespace TrustFinance.Data;

public class TrustFinanceDbContext(DbContextOptions<TrustFinanceDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryRule> CategoryRules => Set<CategoryRule>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<FixedIncomeInvestment> FixedIncomeInvestments => Set<FixedIncomeInvestment>();
    public DbSet<CorporateAction> CorporateActions => Set<CorporateAction>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<WatchItem> Watchlist => Set<WatchItem>();
    public DbSet<PriceAlert> PriceAlerts => Set<PriceAlert>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>
    /// Stamps the audit trail. A property rather than a constructor dependency because the
    /// context is built by a factory from options alone — and because a test that needs the
    /// trail on a known date can set it without a container.
    /// </summary>
    public TimeProvider Clock { get; set; } = TimeProvider.System;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Entities inherit Notifiable, which exposes a Notifications collection. That is
        // validation state produced in memory, not data — without this EF discovers it by
        // convention and demands a primary key for it.
        modelBuilder.Ignore<Notification>();

        modelBuilder.ApplyConfiguration(new UserMap());
        modelBuilder.ApplyConfiguration(new CategoryMap());
        modelBuilder.ApplyConfiguration(new CategoryRuleMap());
        modelBuilder.ApplyConfiguration(new AccountMap());
        modelBuilder.ApplyConfiguration(new TransactionMap());
        modelBuilder.ApplyConfiguration(new RecurringTransactionMap());
        modelBuilder.ApplyConfiguration(new TradeMap());
        modelBuilder.ApplyConfiguration(new FixedIncomeInvestmentMap());
        modelBuilder.ApplyConfiguration(new CorporateActionMap());
        modelBuilder.ApplyConfiguration(new PayoutMap());
        modelBuilder.ApplyConfiguration(new WatchItemMap());
        modelBuilder.ApplyConfiguration(new PriceAlertMap());
        modelBuilder.ApplyConfiguration(new BudgetMap());
        modelBuilder.ApplyConfiguration(new UserSettingsMap());
        modelBuilder.ApplyConfiguration(new AuditEntryMap());

        base.OnModelCreating(modelBuilder);
    }

    // Both overloads take the boolean form, because the parameterless SaveChanges on the
    // base class delegates to it — overriding only the short one would let half the calls
    // in the codebase past the version stamp and the trail.

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var pending = StampVersionsAndCollectAudit();

        if (pending.Count == 0)
            return base.SaveChanges(acceptAllChangesOnSuccess);

        // Two saves, one transaction: the rows go first because an insert has no id to point
        // at until the database has given it one, and the trail follows. A change that
        // committed without its line would be exactly the change nobody can account for, so
        // either both land or neither does. A transaction the caller already opened provides
        // that on its own and is left alone.
        using var transaction = Database.CurrentTransaction is null ? Database.BeginTransaction() : null;

        var written = base.SaveChanges(acceptAllChangesOnSuccess);
        WriteAudit(pending);
        base.SaveChanges(acceptAllChangesOnSuccess);

        transaction?.Commit();
        return written;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var pending = StampVersionsAndCollectAudit();

        if (pending.Count == 0)
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        // See SaveChanges above: the change and the line describing it commit together.
        IDbContextTransaction? transaction = Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var written = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            WriteAudit(pending);
            await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            return written;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    /// <summary>
    /// Raises the version of every changed <see cref="IVersioned"/> row and describes every
    /// changed <see cref="IAuditable"/> one.
    ///
    /// The description is taken here, before the save, because that is the only moment both
    /// the old and the new value exist. The rows themselves are written afterwards: an
    /// insert has no id to point at until the database has assigned one.
    /// </summary>
    private List<PendingAudit> StampVersionsAndCollectAudit()
    {
        var pending = new List<PendingAudit>();

        // Materialized: writing the audit rows below adds entries, and the tracker cannot be
        // enumerated while it is being added to.
        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            if (entry.Entity is IVersioned && entry.State == EntityState.Modified)
                entry.CurrentValues[nameof(IVersioned.Version)] = (int)entry.OriginalValues[nameof(IVersioned.Version)]! + 1;

            if (entry.Entity is not IAuditable auditable)
                continue;

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Created,
                EntityState.Deleted => AuditAction.Deleted,
                _ => AuditAction.Updated
            };

            // A correction that changed nothing is not an event worth a line in the trail.
            var changes = action == AuditAction.Updated ? Describe(entry) : null;
            if (action == AuditAction.Updated && changes is null)
                continue;

            var id = entry.State == EntityState.Added ? 0 : (int)entry.Property("Id").CurrentValue!;
            pending.Add(new PendingAudit(entry, auditable.UserId, entry.Entity.GetType().Name, action, id, changes));
        }

        return pending;
    }

    private void WriteAudit(List<PendingAudit> pending)
    {
        var now = Clock.GetUtcNow();

        foreach (var item in pending)
        {
            // An insert only has its id once the database has handed one out.
            var id = item.EntityId != 0 ? item.EntityId : (int)item.Entry.Property("Id").CurrentValue!;
            AuditEntries.Add(new AuditEntry(item.UserId, item.Entity, id, item.Action, now, item.Changes));
        }
    }

    /// <summary>Every scalar that actually moved, as "Field: old -> new". Null when none did.</summary>
    private static string? Describe(EntityEntry entry)
    {
        var changes = new List<string>();

        foreach (var property in entry.Properties)
        {
            // The version moves on every write by definition, so reporting it would add a
            // line of noise to every entry in the trail and tell nobody anything.
            if (!property.IsModified || property.Metadata.Name == nameof(IVersioned.Version))
                continue;

            var before = property.OriginalValue;
            var after = property.CurrentValue;
            if (Equals(before, after))
                continue;

            changes.Add($"{property.Metadata.Name}: {Format(before)} -> {Format(after)}");
        }

        return changes.Count == 0 ? null : string.Join("; ", changes);
    }

    private static string Format(object? value) => value switch
    {
        null => "—",
        bool flag => flag ? "sim" : "não",
        _ => value.ToString() ?? "—"
    };

    /// <summary>One audited change, held between the stamp pass and the write pass.</summary>
    private sealed record PendingAudit(
        EntityEntry Entry, int UserId, string Entity, AuditAction Action, int EntityId, string? Changes);
}
