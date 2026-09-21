using TrustFinance.Domain.Entities;

namespace TrustFinance.Domain.Investing;

/// <summary>
/// Turns a ledger of trades plus a set of quotes into positions and totals. Pure: no
/// database, no clock, no network — the whole "how much is it yielding" question is
/// decided here and can be tested with a list and a dictionary.
/// </summary>
public static class Portfolio
{
    /// <summary>
    /// Rebuilds every position from <paramref name="trades"/>.
    ///
    /// Average price follows the Brazilian convention: a buy moves it, a sell does not.
    /// Selling banks the difference between proceeds and the average cost of what left,
    /// and leaves the average price of the remainder untouched — which is what makes the
    /// number still mean something after a partial sale.
    ///
    /// Costs are taken seriously in both directions: fees raise the basis on a buy and
    /// reduce proceeds on a sell, because a return that ignores them is not a return.
    /// </summary>
    public static PortfolioSummary Build(
        IEnumerable<Trade> trades,
        IReadOnlyDictionary<string, Quote> quotes)
        => Build(trades, [], [], quotes, asOf: null);

    /// <param name="asOf">The date the trailing twelve months of payouts are counted back from. Null counts every payout.</param>
    public static PortfolioSummary Build(
        IEnumerable<Trade> trades,
        IEnumerable<CorporateAction> actions,
        IEnumerable<Payout> payouts,
        IReadOnlyDictionary<string, Quote> quotes,
        DateOnly? asOf)
    {
        var books = new Dictionary<string, Book>(StringComparer.OrdinalIgnoreCase);

        // Chronological, because average price is path dependent: the same trades in a
        // different order bank a different realized result. A corporate action on the
        // same day as a trade applies first — the trade was already done at post-event
        // prices, so the position it lands on must already be post-event too.
        var timeline = trades
            .Select(t => (t.Date, Order: 1, Trade: (Trade?)t, Action: (CorporateAction?)null, Id: t.Id))
            .Concat(actions.Select(a => (a.Date, Order: 0, Trade: (Trade?)null, Action: (CorporateAction?)a, Id: a.Id)))
            .OrderBy(e => e.Date).ThenBy(e => e.Order).ThenBy(e => e.Id);

        foreach (var e in timeline)
        {
            if (e.Trade is { } t)
            {
                if (!books.TryGetValue(t.Ticker, out var book))
                    books[t.Ticker] = book = new Book(t.Ticker, t.Class);

                book.Apply(t);
            }
            else if (e.Action is { } a && books.TryGetValue(a.Ticker, out var held))
            {
                // An event on a ticker never held is a record with nothing to act on.
                held.Apply(a);
            }
        }

        var since = asOf?.AddMonths(-12);
        var payoutsByTicker = payouts
            .GroupBy(p => p.Ticker, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var trailing = g.Where(p => since is null || p.PaymentDate > since).ToList();
                    return (
                        Total: g.Sum(p => p.NetAmount),
                        Trailing: trailing.Sum(p => p.NetAmount),
                        // Per share, so the yield survives the position growing or shrinking
                        // after the payout: what one share earned over what one share cost.
                        PerShare: trailing.Sum(p => p.Quantity > 0 ? p.NetAmount / p.Quantity : 0m));
                },
                StringComparer.OrdinalIgnoreCase);

        var positions = new List<Position>();
        foreach (var book in books.Values)
        {
            payoutsByTicker.TryGetValue(book.Ticker, out var received);

            // A ticker fully sold still carries its realized result and what it paid out;
            // one that netted to nothing at all is just noise and is dropped.
            if (!book.IsOpen && book.RealizedPnL == 0m && received.Total == 0m)
                continue;

            quotes.TryGetValue(book.Ticker, out var quote);
            positions.Add(new Position(
                book.Ticker,
                book.Class,
                book.Quantity,
                book.AveragePrice,
                Math.Round(book.RealizedPnL, 2),
                book.IsOpen ? quote : null,
                Math.Round(received.Total, 2),
                Math.Round(received.Trailing, 2),
                received.PerShare));
        }

        positions = [.. positions.OrderByDescending(p => p.MarketValue).ThenBy(p => p.Ticker)];

        var open = positions.Where(p => p.IsOpen).ToList();
        var marketValue = open.Sum(p => p.MarketValue);

        var allocation = open
            .GroupBy(p => p.Class)
            .Select(g => new Allocation(
                g.Key,
                g.Sum(p => p.MarketValue),
                marketValue > 0 ? g.Sum(p => p.MarketValue) / marketValue * 100m : 0m))
            .OrderByDescending(a => a.Value)
            .ToList();

        var dayChanges = open.Where(p => p.DayChange is not null).ToList();

        return new PortfolioSummary(
            Positions: positions,
            Allocation: allocation,
            Invested: open.Sum(p => p.Invested),
            MarketValue: marketValue,
            UnrealizedPnL: open.Sum(p => p.UnrealizedPnL),
            RealizedPnL: positions.Sum(p => p.RealizedPnL),
            DayChange: dayChanges.Count > 0 ? dayChanges.Sum(p => p.DayChange!.Value) : null,
            TickersWithoutQuote: open.Count(p => !p.HasQuote),
            PayoutsReceived: positions.Sum(p => p.PayoutsReceived),
            PayoutsTrailingYear: positions.Sum(p => p.PayoutsTrailingYear));
    }

    /// <summary>Every distinct ticker the ledger touches, for asking a provider about them in one call.</summary>
    public static IReadOnlyList<string> Tickers(IEnumerable<Trade> trades) =>
        [.. trades.Select(t => t.Ticker)
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)];
}
