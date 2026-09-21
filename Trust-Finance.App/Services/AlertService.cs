using Microsoft.EntityFrameworkCore;
using TrustFinance.App.Services.Market;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Notifications;

namespace TrustFinance.App.Services;

/// <summary>
/// Price alerts and the sweep that fires them. There is no background worker: alerts are
/// evaluated whenever the user is looking, which for a locally-run app is the only moment
/// a notification could be seen anyway.
/// </summary>
public class AlertService(
    IDbContextFactory<TrustFinanceDbContext> factory,
    MarketData market,
    TimeProvider clock)
{
    public async Task<List<PriceAlert>> GetAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.PriceAlerts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.TriggeredAt == null ? 0 : 1)
            .ThenBy(a => a.Ticker)
            .ToListAsync();
    }

    public async Task<Result> AddAsync(string ticker, AssetClass assetClass, AlertDirection direction, decimal target, int userId)
    {
        var alert = new PriceAlert(ticker, assetClass, direction, target, userId);
        if (alert.IsInvalid)
            return Result.Fail(alert);

        await using var db = await factory.CreateDbContextAsync();
        db.PriceAlerts.Add(alert);
        await db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var alert = await db.PriceAlerts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (alert is null)
            return Result.Fail(nameof(PriceAlert), "Alerta não encontrado");

        db.PriceAlerts.Remove(alert);
        await db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task DismissAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.PriceAlerts
            .Where(a => a.Id == id && a.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Dismissed, true));
    }

    public async Task DismissAllAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.PriceAlerts
            .Where(a => a.UserId == userId && a.TriggeredAt != null && !a.Dismissed)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Dismissed, true));
    }

    /// <summary>
    /// Stamps every armed alert whose target the current price has crossed. Returns the
    /// ones that fired on this sweep, so a caller can react to new news specifically.
    /// </summary>
    public async Task<IReadOnlyList<PriceAlert>> EvaluateAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var armed = await db.PriceAlerts
            .Where(a => a.UserId == userId && a.TriggeredAt == null)
            .ToListAsync();

        if (armed.Count == 0)
            return [];

        var tickers = armed.Select(a => a.Ticker).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var quotes = await market.QuotesAsync(userId, tickers);

        var now = clock.GetUtcNow().UtcDateTime;
        var fired = new List<PriceAlert>();

        foreach (var alert in armed)
        {
            // Whether the price crosses the target is the alert's own rule, not this
            // service's — asking the entity keeps the two from ever disagreeing.
            if (quotes.TryGetValue(alert.Ticker, out var quote) && alert.Trigger(quote.Price, now))
                fired.Add(alert);
        }

        if (fired.Count > 0)
            await db.SaveChangesAsync();

        return fired;
    }
}
