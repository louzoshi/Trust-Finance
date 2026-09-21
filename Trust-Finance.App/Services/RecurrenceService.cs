using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Notifications;

namespace TrustFinance.App.Services;

public class RecurrenceService(IDbContextFactory<TrustFinanceDbContext> factory, TimeProvider clock)
{
    public async Task<List<RecurringTransaction>> GetAllAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.RecurringTransactions
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.NextDate)
            .ThenBy(r => r.Id)
            .ToListAsync();
    }

    public async Task<RecurringTransaction?> GetByIdAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.RecurringTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
    }

    /// <summary>
    /// Turns every occurrence that has fallen due into a real transaction. Any page that
    /// reads the ledger calls this first, so a salary dated the 5th is on the books the
    /// first time the app is opened on or after the 5th — there is no background job to
    /// keep alive in a single-process desktop app. Returns how many rows were posted.
    /// </summary>
    public async Task<int> PostDueAsync(int userId)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

        await using var db = await factory.CreateDbContextAsync();

        var due = await db.RecurringTransactions
            .Where(r => r.UserId == userId && r.NextDate <= today)
            .ToListAsync();

        var posted = 0;
        foreach (var recurrence in due)
        {
            var transactions = recurrence.PostDue(today);
            db.Transactions.AddRange(transactions);
            posted += transactions.Count;
        }

        if (posted > 0)
            await db.SaveChangesAsync();

        return posted;
    }

    public async Task<Result<RecurringTransaction>> CreateAsync(RecurringTransaction recurrence)
    {
        if (recurrence.IsInvalid)
            return Result<RecurringTransaction>.Fail(recurrence);

        await using var db = await factory.CreateDbContextAsync();

        if (!await CategoryIsReachableAsync(db, recurrence.CategoryId, recurrence.UserId))
            return Result<RecurringTransaction>.Fail(nameof(RecurringTransaction.CategoryId), "Categoria não encontrada");
        if (!await db.Accounts.AnyAsync(a => a.Id == recurrence.AccountId && a.UserId == recurrence.UserId))
            return Result<RecurringTransaction>.Fail(nameof(RecurringTransaction.AccountId), "Conta não encontrada");

        db.RecurringTransactions.Add(recurrence);
        await db.SaveChangesAsync();
        return Result<RecurringTransaction>.Ok(recurrence);
    }

    public async Task<Result> UpdateAsync(int id, RecurringTransaction corrected)
    {
        if (corrected.IsInvalid)
            return Result.Fail(corrected);

        await using var db = await factory.CreateDbContextAsync();

        var recurrence = await db.RecurringTransactions.FirstOrDefaultAsync(r => r.Id == id && r.UserId == corrected.UserId);
        if (recurrence is null)
            return Result.Fail(nameof(RecurringTransaction), "Recorrência não encontrada");

        if (!await CategoryIsReachableAsync(db, corrected.CategoryId, corrected.UserId))
            return Result.Fail(nameof(RecurringTransaction.CategoryId), "Categoria não encontrada");
        if (!await db.Accounts.AnyAsync(a => a.Id == corrected.AccountId && a.UserId == corrected.UserId))
            return Result.Fail(nameof(RecurringTransaction.AccountId), "Conta não encontrada");

        recurrence.CorrectTo(corrected);
        if (recurrence.IsInvalid)
            return Result.Fail(recurrence);

        await db.SaveChangesAsync();
        return Result.Ok();
    }

    /// <summary>Stops the recurrence. What it already posted stays on the ledger.</summary>
    public async Task<Result> DeleteAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var recurrence = await db.RecurringTransactions.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
        if (recurrence is null)
            return Result.Fail(nameof(RecurringTransaction), "Recorrência não encontrada");

        db.RecurringTransactions.Remove(recurrence);
        await db.SaveChangesAsync();
        return Result.Ok();
    }

    // Same reasoning as TransactionService: a foreign key failure is not a readable
    // rejection, and another user's category must not be reachable by id.
    private static Task<bool> CategoryIsReachableAsync(TrustFinanceDbContext db, int categoryId, int userId)
        => db.Categories.AsNoTracking().AnyAsync(c => c.Id == categoryId && c.UserId == userId);
}
