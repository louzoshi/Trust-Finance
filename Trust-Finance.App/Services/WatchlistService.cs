using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Notifications;
using TrustFinance.Domain.ValueObjects;

namespace TrustFinance.App.Services;

public class WatchlistService(IDbContextFactory<TrustFinanceDbContext> factory, TimeProvider clock)
{
    public async Task<List<WatchItem>> GetAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Watchlist
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.Ticker)
            .ToListAsync();
    }

    public async Task<Result> AddAsync(string ticker, AssetClass assetClass, int userId)
    {
        var item = new WatchItem(ticker, assetClass, userId, clock.GetUtcNow().UtcDateTime);
        if (item.IsInvalid)
            return Result.Fail(item);

        await using var db = await factory.CreateDbContextAsync();

        // Following the same ticker twice is a no-op, not an error: the user pressed a
        // button whose outcome they already have.
        if (await db.Watchlist.AnyAsync(w => w.UserId == userId && w.Ticker == item.Ticker))
            return Result.Ok();

        db.Watchlist.Add(item);
        await db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> RemoveAsync(string ticker, int userId)
    {
        var symbol = new Ticker(ticker);
        if (symbol.IsInvalid)
            return Result.Fail(symbol);

        await using var db = await factory.CreateDbContextAsync();
        await db.Watchlist
            .Where(w => w.UserId == userId && w.Ticker == symbol.Value)
            .ExecuteDeleteAsync();

        return Result.Ok();
    }
}
