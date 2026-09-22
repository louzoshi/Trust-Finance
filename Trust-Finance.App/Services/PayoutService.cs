using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Notifications;

namespace TrustFinance.App.Services;

public class PayoutService(IDbContextFactory<TrustFinanceDbContext> factory)
{
    public async Task<List<Payout>> GetAllAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Payouts
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.Id)
            .ToListAsync();
    }

    public async Task<Result<Payout>> CreateAsync(Payout payout)
    {
        if (payout.IsInvalid)
            return Result<Payout>.Fail(payout);

        await using var db = await factory.CreateDbContextAsync();
        db.Payouts.Add(payout);
        await db.SaveChangesAsync();
        return Result<Payout>.Ok(payout);
    }

    public async Task<Result> UpdateAsync(int id, Payout corrected, int expectedVersion)
    {
        if (corrected.IsInvalid)
            return Result.Fail(corrected);

        await using var db = await factory.CreateDbContextAsync();

        var payout = await db.Payouts.FirstOrDefaultAsync(p => p.Id == id && p.UserId == corrected.UserId);
        if (payout is null)
            return Result.Fail(nameof(Payout), "Provento não encontrado");

        // The version the form was opened on. A correction made against an older one is
        // refused rather than laid on top of somebody else's.
        if (payout.Version != expectedVersion)
            return Concurrency.Conflict();

        payout.CorrectTo(corrected);
        if (payout.IsInvalid)
            return Result.Fail(payout);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Written between the check above and this line — the window it cannot see.
            return Concurrency.Conflict();
        }

        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var payout = await db.Payouts.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (payout is null)
            return Result.Fail(nameof(Payout), "Provento não encontrado");

        db.Payouts.Remove(payout);
        await db.SaveChangesAsync();
        return Result.Ok();
    }
}
