using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Notifications;

namespace TrustFinance.App.Services;

public class TransactionService(IDbContextFactory<TrustFinanceDbContext> factory)
{
    public async Task<List<Transaction>> GetAllAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToListAsync();
    }

    /// <summary>The user's transactions inside <paramref name="period"/>, both ends inclusive, newest first; optionally on one account.</summary>
    public async Task<List<Transaction>> GetAsync(int userId, Period period, int? accountId = null)
    {
        await using var db = await factory.CreateDbContextAsync();

        var query = db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId);

        if (accountId is { } account)
            query = query.Where(t => t.AccountId == account);

        if (period.From is { } from)
            query = query.Where(t => t.Date >= from);
        if (period.To is { } to)
            query = query.Where(t => t.Date <= to);

        return await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToListAsync();
    }

    public async Task<Transaction?> GetByIdAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
    }

    public async Task<Result<Transaction>> CreateAsync(Transaction transaction)
    {
        if (transaction.IsInvalid)
            return Result<Transaction>.Fail(transaction);

        await using var db = await factory.CreateDbContextAsync();

        if (!await CategoryIsReachableAsync(db, transaction.CategoryId, transaction.UserId))
            return Result<Transaction>.Fail(nameof(Transaction.CategoryId), "Categoria não encontrada");
        if (!await AccountIsReachableAsync(db, transaction.AccountId, transaction.UserId))
            return Result<Transaction>.Fail(nameof(Transaction.AccountId), "Conta não encontrada");

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
        return Result<Transaction>.Ok(transaction);
    }

    /// <summary>All instalments of one purchase, saved together or not at all.</summary>
    public async Task<Result> CreateInstallmentsAsync(IReadOnlyList<Transaction> installments)
    {
        if (installments.Count == 0)
            return Result.Fail(nameof(Transaction), "Nada a lançar");

        var invalid = installments.FirstOrDefault(t => t.IsInvalid);
        if (invalid is not null)
            return Result.Fail(invalid);

        var first = installments[0];
        await using var db = await factory.CreateDbContextAsync();

        if (!await CategoryIsReachableAsync(db, first.CategoryId, first.UserId))
            return Result.Fail(nameof(Transaction.CategoryId), "Categoria não encontrada");
        if (!await AccountIsReachableAsync(db, first.AccountId, first.UserId))
            return Result.Fail(nameof(Transaction.AccountId), "Conta não encontrada");

        db.Transactions.AddRange(installments);
        await db.SaveChangesAsync();
        return Result.Ok();
    }

    /// <summary>Both halves of a transfer, saved together or not at all.</summary>
    public async Task<Result> CreateTransferAsync(Transaction outgoing, Transaction incoming)
    {
        if (outgoing.IsInvalid)
            return Result.Fail(outgoing);
        if (incoming.IsInvalid)
            return Result.Fail(incoming);

        await using var db = await factory.CreateDbContextAsync();

        if (!await CategoryIsReachableAsync(db, outgoing.CategoryId, outgoing.UserId))
            return Result.Fail(nameof(Transaction.CategoryId), "Categoria não encontrada");
        if (!await AccountIsReachableAsync(db, outgoing.AccountId, outgoing.UserId)
            || !await AccountIsReachableAsync(db, incoming.AccountId, incoming.UserId))
            return Result.Fail(nameof(Transaction.AccountId), "Conta não encontrada");

        db.Transactions.AddRange(outgoing, incoming);
        await db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> UpdateAsync(int id, Transaction corrected)
    {
        if (corrected.IsInvalid)
            return Result.Fail(corrected);

        await using var db = await factory.CreateDbContextAsync();

        var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == corrected.UserId);
        if (transaction is null)
            return Result.Fail(nameof(Transaction), "Lançamento não encontrado");

        if (!await CategoryIsReachableAsync(db, corrected.CategoryId, corrected.UserId))
            return Result.Fail(nameof(Transaction.CategoryId), "Categoria não encontrada");
        if (!await AccountIsReachableAsync(db, corrected.AccountId, corrected.UserId))
            return Result.Fail(nameof(Transaction.AccountId), "Conta não encontrada");

        transaction.CorrectTo(corrected);
        if (transaction.IsInvalid)
            return Result.Fail(transaction);

        await db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (transaction is null)
            return Result.Fail(nameof(Transaction), "Lançamento não encontrado");

        // Half a transfer would leave money that left one account and arrived nowhere.
        if (transaction.TransferId is { } transfer)
            db.Transactions.RemoveRange(db.Transactions.Where(t => t.TransferId == transfer && t.UserId == userId));
        else
            db.Transactions.Remove(transaction);

        await db.SaveChangesAsync();
        return Result.Ok();
    }

    /// <summary>
    /// CategoryId is a foreign key: an unknown value fails at SaveChanges with a database
    /// error rather than a readable rejection, so it is validated up front. The check is
    /// scoped to the caller as well — pointing a transaction at somebody else's category
    /// would leak that category's name back through the transaction list.
    /// </summary>
    private static Task<bool> CategoryIsReachableAsync(TrustFinanceDbContext db, int categoryId, int userId)
        => db.Categories.AsNoTracking().AnyAsync(c => c.Id == categoryId && c.UserId == userId);

    private static Task<bool> AccountIsReachableAsync(TrustFinanceDbContext db, int accountId, int userId)
        => db.Accounts.AsNoTracking().AnyAsync(a => a.Id == accountId && a.UserId == userId);
}
