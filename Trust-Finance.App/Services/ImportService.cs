using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Banking;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Notifications;

namespace TrustFinance.App.Services;

public enum ImportStatus
{
    /// <summary>Nothing on the ledger looks like it: will be created.</summary>
    New = 1,

    /// <summary>This exact bank id is already on the ledger: will be skipped.</summary>
    AlreadyImported = 2,

    /// <summary>A hand-typed row with the same amount close to the same date: will be tied to it rather than duplicated.</summary>
    MatchesExisting = 3
}

/// <summary>One statement line as the preview shows it, with what the import will do about it.</summary>
public sealed class ImportLine
{
    public required OfxTransaction Source { get; init; }
    public required ImportStatus Status { get; init; }
    public Transaction? Existing { get; init; }
    public int CategoryId { get; set; }
    public bool Include { get; set; } = true;

    public TransactionType Type => Source.IsCredit ? TransactionType.Income : TransactionType.Expense;
    public decimal Amount => Math.Abs(Source.Amount);
}

public sealed record ImportPreview(OfxStatement Statement, IReadOnlyList<ImportLine> Lines)
{
    public int NewCount => Lines.Count(l => l.Status == ImportStatus.New);
    public int SkippedCount => Lines.Count(l => l.Status == ImportStatus.AlreadyImported);
    public int MatchedCount => Lines.Count(l => l.Status == ImportStatus.MatchesExisting);
}

public sealed record ImportOutcome(int Created, int Reconciled, int Skipped, int RulesLearned);

/// <summary>
/// Brings a bank statement onto the ledger without ever creating the same line twice.
///
/// Three things make that true. The bank's own id (FITID) is stored on every imported
/// row under a unique index, so a second import of the same file finds every line
/// already there. A row the user typed by hand before importing — same account, same
/// amount, within two days — is recognised and tied to the bank line instead of being
/// duplicated: that is reconciliation. And the whole commit runs in one transaction, so
/// a failure halfway leaves nothing behind to clean up.
/// </summary>
public class ImportService(IDbContextFactory<TrustFinanceDbContext> factory, CategoryRuleService rules)
{
    /// <summary>How far apart the bank's posting date and the user's typed date may be and still be the same thing.</summary>
    public const int MatchWindowDays = 2;

    public async Task<ImportPreview> PreviewAsync(string content, int accountId, int userId)
    {
        var statement = Ofx.Parse(content);

        await using var db = await factory.CreateDbContextAsync();

        var fitIds = statement.Transactions.Select(t => t.FitId).ToList();
        var known = await db.Transactions.AsNoTracking()
            .Where(t => t.UserId == userId && t.AccountId == accountId && t.ExternalId != null && fitIds.Contains(t.ExternalId))
            .Select(t => t.ExternalId!)
            .ToHashSetAsync();

        // Candidates for reconciliation: rows on this account with no bank id yet, in
        // the statement's period widened by the match window.
        var from = (statement.From ?? statement.Transactions.Min(t => t.Date)).AddDays(-MatchWindowDays);
        var to = (statement.To ?? statement.Transactions.Max(t => t.Date)).AddDays(MatchWindowDays);
        var untied = await db.Transactions.AsNoTracking()
            .Where(t => t.UserId == userId && t.AccountId == accountId && t.ExternalId == null && t.Date >= from && t.Date <= to)
            .ToListAsync();

        var userRules = await rules.GetAllAsync(userId);
        var claimed = new HashSet<int>();
        var lines = new List<ImportLine>();

        foreach (var source in statement.Transactions.OrderBy(t => t.Date))
        {
            if (known.Contains(source.FitId))
            {
                lines.Add(new ImportLine { Source = source, Status = ImportStatus.AlreadyImported, Include = false });
                continue;
            }

            var type = source.IsCredit ? TransactionType.Income : TransactionType.Expense;
            var match = untied
                .Where(t => !claimed.Contains(t.Id)
                            && t.Type == type
                            && t.Amount == Math.Abs(source.Amount)
                            && Math.Abs(t.Date.DayNumber - source.Date.DayNumber) <= MatchWindowDays)
                .OrderBy(t => Math.Abs(t.Date.DayNumber - source.Date.DayNumber))
                .FirstOrDefault();

            if (match is not null)
            {
                claimed.Add(match.Id);
                lines.Add(new ImportLine { Source = source, Status = ImportStatus.MatchesExisting, Existing = match, CategoryId = match.CategoryId });
                continue;
            }

            lines.Add(new ImportLine
            {
                Source = source,
                Status = ImportStatus.New,
                CategoryId = CategoryRuleService.Suggest(userRules, source.Memo) ?? 0
            });
        }

        return new ImportPreview(statement, lines);
    }

    public async Task<Result<ImportOutcome>> CommitAsync(ImportPreview preview, int accountId, int userId, bool learnRules)
    {
        var included = preview.Lines.Where(l => l.Include && l.Status != ImportStatus.AlreadyImported).ToList();

        var uncategorized = included.Where(l => l.Status == ImportStatus.New && l.CategoryId <= 0).ToList();
        if (uncategorized.Count > 0)
            return Result<ImportOutcome>.Fail(nameof(ImportLine.CategoryId),
                uncategorized.Count == 1 ? "Uma linha está sem categoria" : $"{uncategorized.Count} linhas estão sem categoria");

        await using var db = await factory.CreateDbContextAsync();

        if (!await db.Accounts.AnyAsync(a => a.Id == accountId && a.UserId == userId))
            return Result<ImportOutcome>.Fail(nameof(Transaction.AccountId), "Conta não encontrada");

        var categoryIds = included.Select(l => l.CategoryId).Distinct().ToList();
        var reachable = await db.Categories.Where(c => c.UserId == userId && categoryIds.Contains(c.Id)).Select(c => c.Id).ToHashSetAsync();
        if (categoryIds.Any(id => !reachable.Contains(id)))
            return Result<ImportOutcome>.Fail(nameof(Transaction.CategoryId), "Categoria não encontrada");

        await using var tx = await db.Database.BeginTransactionAsync();

        var created = 0;
        var reconciled = 0;
        var learned = 0;
        var existingRules = learnRules ? await db.CategoryRules.Where(r => r.UserId == userId).ToListAsync() : [];

        foreach (var line in included)
        {
            if (line.Status == ImportStatus.MatchesExisting && line.Existing is not null)
            {
                var row = await db.Transactions.FirstOrDefaultAsync(t => t.Id == line.Existing.Id && t.UserId == userId);
                if (row is null || row.ExternalId is not null)
                    continue;

                row.Reconcile(line.Source.FitId);
                reconciled++;
                continue;
            }

            var transaction = Transaction.Imported(
                line.Source.Memo, line.Amount, line.Source.Date, line.Type, line.CategoryId, accountId, userId, line.Source.FitId);

            if (transaction.IsInvalid)
                return Result<ImportOutcome>.Fail(transaction);

            db.Transactions.Add(transaction);
            created++;

            if (learnRules && CategoryRuleService.Suggest(existingRules, line.Source.Memo) != line.CategoryId)
            {
                var rule = new CategoryRule(line.Source.Memo, line.CategoryId, userId);
                if (rule.IsValid && existingRules.All(r => r.Pattern != rule.Pattern))
                {
                    db.CategoryRules.Add(rule);
                    existingRules.Add(rule);
                    learned++;
                }
            }
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Result<ImportOutcome>.Ok(new ImportOutcome(created, reconciled, preview.SkippedCount, learned));
    }
}
