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

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
        return Result<Transaction>.Ok(transaction);
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
}
