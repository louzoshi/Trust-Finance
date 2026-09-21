using TrustFinance.Domain.Entities;

namespace TrustFinance.Domain.Investing;

/// <summary>
/// What the user holds in one ticker right now, rebuilt from the trade ledger.
/// </summary>
public sealed record Position(
    string Ticker,
    AssetClass Class,
    decimal Quantity,
    decimal AveragePrice,
    decimal RealizedPnL,
    Quote? Quote,
    decimal PayoutsReceived = 0m,
    decimal PayoutsTrailingYear = 0m,
    decimal PayoutsPerShareTrailingYear = 0m)
{
    /// <summary>What it cost to build what is still held, average price times quantity.</summary>
    public decimal Invested => Math.Round(Quantity * AveragePrice, 2);

    /// <summary>Worth at the last known price, or cost when no quote is available.</summary>
    public decimal MarketValue => Quote is not null
        ? Math.Round(Quantity * Quote.Price, 2)
        : Invested;

    public bool HasQuote => Quote is not null;

    /// <summary>Profit still on paper. Meaningless without a quote, hence zero when there is none.</summary>
    public decimal UnrealizedPnL => Quote is not null ? MarketValue - Invested : 0m;

    public decimal? UnrealizedPercent =>
        Quote is not null && Invested > 0 ? UnrealizedPnL / Invested * 100m : null;

    /// <summary>Everything this ticker has produced: paper profit, what past sales banked, and what it paid out.</summary>
    public decimal TotalPnL => UnrealizedPnL + RealizedPnL + PayoutsReceived;

    /// <summary>
    /// Yield on cost: what one share paid out over the last twelve months, over what one
    /// share cost. Measured against cost rather than price because that is the question
    /// the holder has — what is my money earning — not what a buyer today would get.
    /// Per share rather than in total, so selling half the position after a payout does
    /// not double the number.
    /// </summary>
    public decimal? YieldOnCost => IsOpen && AveragePrice > 0 ? PayoutsPerShareTrailingYear / AveragePrice * 100m : null;

    /// <summary>Move since the previous close, for the whole position.</summary>
    public decimal? DayChange => Quote?.Change is { } c ? Math.Round(Quantity * c, 2) : null;

    public bool IsOpen => Quantity > 0;
}

/// <summary>One slice of the allocation pie.</summary>
public sealed record Allocation(AssetClass Class, decimal Value, decimal Percent);

public sealed record PortfolioSummary(
    IReadOnlyList<Position> Positions,
    IReadOnlyList<Allocation> Allocation,
    decimal Invested,
    decimal MarketValue,
    decimal UnrealizedPnL,
    decimal RealizedPnL,
    decimal? DayChange,
    int TickersWithoutQuote,
    decimal PayoutsReceived = 0m,
    decimal PayoutsTrailingYear = 0m)
{
    public decimal TotalPnL => UnrealizedPnL + RealizedPnL + PayoutsReceived;

    /// <summary>The open positions' yields on cost, weighted by what each one cost.</summary>
    public decimal? YieldOnCost
    {
        get
        {
            var open = Positions.Where(p => p.IsOpen && p.YieldOnCost is not null).ToList();
            var cost = open.Sum(p => p.Invested);
            return cost > 0 ? open.Sum(p => p.YieldOnCost!.Value * p.Invested) / cost : null;
        }
    }

    public decimal? ReturnPercent => Invested > 0 ? UnrealizedPnL / Invested * 100m : null;

    public decimal? DayChangePercent
    {
        get
        {
            var opening = MarketValue - (DayChange ?? 0m);
            return DayChange is { } d && opening > 0 ? d / opening * 100m : null;
        }
    }

    public bool IsEmpty => Positions.Count == 0;

    /// <summary>True when some holdings are priced at cost because no quote arrived — the totals are understated.</summary>
    public bool IsPartiallyPriced => TickersWithoutQuote > 0;
}
