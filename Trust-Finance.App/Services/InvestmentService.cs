using Microsoft.EntityFrameworkCore;
using TrustFinance.App.Services.Market;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Investing;
using TrustFinance.Domain.Notifications;
using TrustFinance.Domain.Tax;

namespace TrustFinance.App.Services;

/// <summary>
/// The trade ledger and the portfolio built from it. Like every other service here, each
/// method is scoped to the calling user: a trade id that belongs to somebody else is
/// indistinguishable from one that does not exist.
/// </summary>
public class InvestmentService(
    IDbContextFactory<TrustFinanceDbContext> factory,
    MarketData market,
    BcbSeries cdi,
    TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

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
        var (trades, actions, payouts) = await LedgerAsync(userId);
        var quotes = await market.QuotesAsync(userId, Portfolio.Tickers(trades));
        return Portfolio.Build(trades, actions, payouts, quotes, Today);
    }

    /// <summary>Month by month, what the sales owed the Receita and why.</summary>
    public async Task<IReadOnlyList<MonthlyTax>> GetTaxReportAsync(int userId)
    {
        var (trades, actions, _) = await LedgerAsync(userId);
        return CapitalGains.Compute(trades, actions);
    }

    /// <summary>The portfolio against the CDI over the days the money was actually invested.</summary>
    public async Task<(PerformanceSummary Summary, bool CdiIsLive)> GetPerformanceAsync(int userId)
    {
        var (trades, actions, payouts) = await LedgerAsync(userId);
        var flows = Performance.CashFlows(trades, payouts);

        if (flows.Count == 0)
            return (Performance.Summarize(flows, 0m, [], Today), true);

        var quotes = await market.QuotesAsync(userId, Portfolio.Tickers(trades));
        var marketValue = Portfolio.Build(trades, actions, payouts, quotes, Today).MarketValue;

        var indicators = await market.IndicatorsAsync(userId);
        var (rates, live) = await cdi.GetAsync(flows[0].Date, Today, indicators.CdiAnnual);

        return (Performance.Summarize(flows, marketValue, rates, Today), live);
    }

    private async Task<(List<Trade> Trades, List<CorporateAction> Actions, List<Payout> Payouts)> LedgerAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var trades = await db.Trades.AsNoTracking().Where(t => t.UserId == userId).ToListAsync();
        var actions = await db.CorporateActions.AsNoTracking().Where(a => a.UserId == userId).ToListAsync();
        var payouts = await db.Payouts.AsNoTracking().Where(p => p.UserId == userId).ToListAsync();

        return (trades, actions, payouts);
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

    public async Task<Result> UpdateTradeAsync(int id, Trade corrected, int expectedVersion)
    {
        if (corrected.IsInvalid)
            return Result.Fail(corrected);

        await using var db = await factory.CreateDbContextAsync();

        var trade = await db.Trades.FirstOrDefaultAsync(t => t.Id == id && t.UserId == corrected.UserId);
        if (trade is null)
            return Result.Fail(nameof(Trade), "Operação não encontrada");

        // The version the form was opened on. A correction made against an older one is
        // refused rather than laid on top of somebody else's.
        if (trade.Version != expectedVersion)
            return Concurrency.Conflict();

        trade.CorrectTo(corrected);
        if (trade.IsInvalid)
            return Result.Fail(trade);

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
