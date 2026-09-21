using Microsoft.EntityFrameworkCore;
using TrustFinance.App.Services.Market;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Investing;
using TrustFinance.Domain.Notifications;

namespace TrustFinance.App.Services;

/// <summary>
/// The trade ledger and the portfolio built from it. Like every other service here, each
/// method is scoped to the calling user: a trade id that belongs to somebody else is
/// indistinguishable from one that does not exist.
/// </summary>
public class InvestmentService(IDbContextFactory<TrustFinanceDbContext> factory, MarketData market)
{
    public async Task<List<Trade>> GetTradesAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Trades
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToListAsync();
    }

    /// <summary>Positions and totals, priced with whatever the market provider could supply.</summary>
    public async Task<PortfolioSummary> GetPortfolioAsync(int userId)
    {
        var trades = await GetTradesAsync(userId);
        var quotes = await market.QuotesAsync(userId, Portfolio.Tickers(trades));
        return Portfolio.Build(trades, quotes);
    }

    public async Task<Result<Trade>> AddTradeAsync(Trade trade)
    {
        if (trade.IsInvalid)
            return Result<Trade>.Fail(trade);

        await using var db = await factory.CreateDbContextAsync();
        db.Trades.Add(trade);
        await db.SaveChangesAsync();
        return Result<Trade>.Ok(trade);
    }

    public async Task<Result> UpdateTradeAsync(int id, Trade corrected)
    {
        if (corrected.IsInvalid)
            return Result.Fail(corrected);

        await using var db = await factory.CreateDbContextAsync();

        var trade = await db.Trades.FirstOrDefaultAsync(t => t.Id == id && t.UserId == corrected.UserId);
        if (trade is null)
            return Result.Fail(nameof(Trade), "Operação não encontrada");

        trade.CorrectTo(corrected);
        if (trade.IsInvalid)
            return Result.Fail(trade);

        await db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteTradeAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var trade = await db.Trades.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (trade is null)
            return Result.Fail(nameof(Trade), "Operação não encontrada");

        db.Trades.Remove(trade);
        await db.SaveChangesAsync();
        return Result.Ok();
    }
}
