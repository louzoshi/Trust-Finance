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
    {
        var books = new Dictionary<string, Book>(StringComparer.OrdinalIgnoreCase);

        // Chronological, because average price is path dependent: the same trades in a
        // different order bank a different realized result.
        foreach (var t in trades.OrderBy(t => t.Date).ThenBy(t => t.Id))
        {
            if (!books.TryGetValue(t.Ticker, out var book))
                books[t.Ticker] = book = new Book(t.Ticker, t.Class);

            book.Apply(t);
        }

        var positions = new List<Position>();
        foreach (var book in books.Values)
        {
            // A ticker fully sold still carries its realized result; one that netted to
            // nothing at all is just noise and is dropped.
            if (!book.IsOpen && book.RealizedPnL == 0m)
                continue;

            quotes.TryGetValue(book.Ticker, out var quote);
            positions.Add(new Position(
                book.Ticker,
                book.Class,
                book.Quantity,
                book.AveragePrice,
                Math.Round(book.RealizedPnL, 2),
                book.IsOpen ? quote : null));
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
            TickersWithoutQuote: open.Count(p => !p.HasQuote));
    }

    /// <summary>Every distinct ticker the ledger touches, for asking a provider about them in one call.</summary>
    public static IReadOnlyList<string> Tickers(IEnumerable<Trade> trades) =>
        [.. trades.Select(t => t.Ticker)
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)];

    /// <summary>Running average price, quantity and banked result for one ticker.</summary>
    private sealed class Book(string ticker, AssetClass assetClass)
    {
        public string Ticker { get; } = ticker;
        public AssetClass Class { get; private set; } = assetClass;
        public decimal Quantity { get; private set; }
        public decimal AveragePrice { get; private set; }
        public decimal RealizedPnL { get; private set; }

        public bool IsOpen => Quantity > 0;

        public void Apply(Trade t)
        {
            // A later trade is the better witness to what the asset is: a ticker first
            // entered under the wrong class gets corrected by the next one.
            Class = t.Class;

            if (t.Side == TradeSide.Buy)
            {
                var cost = t.Gross + t.Fees;
                var total = Quantity + t.Quantity;
                AveragePrice = total > 0 ? (Quantity * AveragePrice + cost) / total : 0m;
                Quantity = total;
                return;
            }

            // Selling more than is held would produce a negative position and a nonsense
            // average. Cap it: the ledger is wrong, but the summary stays readable.
            var sold = Math.Min(t.Quantity, Quantity);
            if (sold <= 0)
                return;

            var proceeds = sold * t.UnitPrice - t.Fees;
            RealizedPnL += proceeds - sold * AveragePrice;
            Quantity -= sold;

            if (Quantity == 0)
                AveragePrice = 0m;
        }
    }
}
