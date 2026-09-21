using Microsoft.EntityFrameworkCore;
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
    public DbSet<CorporateAction> CorporateActions => Set<CorporateAction>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<WatchItem> Watchlist => Set<WatchItem>();
    public DbSet<PriceAlert> PriceAlerts => Set<PriceAlert>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

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
        modelBuilder.ApplyConfiguration(new CorporateActionMap());
        modelBuilder.ApplyConfiguration(new PayoutMap());
        modelBuilder.ApplyConfiguration(new WatchItemMap());
        modelBuilder.ApplyConfiguration(new PriceAlertMap());
        modelBuilder.ApplyConfiguration(new BudgetMap());
        modelBuilder.ApplyConfiguration(new UserSettingsMap());

        base.OnModelCreating(modelBuilder);
    }
}
