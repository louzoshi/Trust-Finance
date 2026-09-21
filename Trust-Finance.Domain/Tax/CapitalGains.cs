using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Investing;

namespace TrustFinance.Domain.Tax;

/// <summary>
/// Which set of rules a result falls under. Losses only ever offset gains inside the
/// same bucket — a bad month in FIIs does not shelter a good month in stocks.
/// </summary>
public enum TaxBucket
{
    /// <summary>Operações comuns: stocks, ETFs and BDRs held overnight. 15%.</summary>
    Common = 1,

    /// <summary>Bought and sold the same day. 20%, no exemption of any kind.</summary>
    DayTrade = 2,

    /// <summary>Real-estate funds, day trade or not. 20%, no exemption.</summary>
    RealEstateFund = 3,

    /// <summary>Crypto. 15% above R$ 35.000 of monthly sales.</summary>
    Crypto = 4
}

public static class TaxBuckets
{
    public static decimal Rate(this TaxBucket bucket) => bucket switch
    {
        TaxBucket.DayTrade => 0.20m,
        TaxBucket.RealEstateFund => 0.20m,
        _ => 0.15m
    };
}

/// <summary>One bucket's numbers for one month.</summary>
public sealed record BucketResult(
    TaxBucket Bucket,
    decimal Sales,
    decimal Result,
    decimal ExemptGain,
    decimal LossOffset,
    decimal TaxableBase,
    decimal Tax,
    decimal LossCarriedForward);

/// <summary>What a month owes, and why.</summary>
public sealed record MonthlyTax(
    int Year,
    int Month,
    decimal StockSales,
    bool StockGainsExempt,
    decimal CryptoSales,
    bool CryptoGainsExempt,
    IReadOnlyList<BucketResult> Buckets,
    decimal Withheld,
    decimal WithheldCreditUsed,
    decimal TaxDue,
    decimal DarfAmount,
    decimal DarfDeferredIn,
    bool DarfDeferred,
    DateOnly DarfDueDate)
{
    public DateOnly FirstDay => new(Year, Month, 1);

    /// <summary>The month's tax net of withholding — what a DARF would carry before the R$ 10 floor.</summary>
    public decimal NetTax => TaxDue - WithheldCreditUsed;

    public BucketResult? this[TaxBucket bucket] => Buckets.FirstOrDefault(b => b.Bucket == bucket);
}

/// <summary>
/// Monthly income-tax computation for an individual trading on B3, following the rules
/// in force for capital gains on variable income (IN RFB 1585/2015 and the annual
/// "Perguntas e Respostas" of the Receita Federal).
///
/// The rules encoded here:
///
/// - Day trade is any quantity bought and sold on the same day in the same ticker, up
///   to the smaller side. It is its own bucket at 20%, with no exemption.
/// - Stock sales (swing) of up to R$ 20.000 in a month make that month's stock gains
///   exempt. The ceiling is on sales, not gains; ETFs and BDRs do not count towards it
///   and are not exempt. Losses in an exempt month still carry forward.
/// - Crypto has the same shape with a R$ 35.000 ceiling.
/// - FIIs are taxed at 20% whatever the holding period, no exemption.
/// - Losses offset later gains in the same bucket only, with no expiry.
/// - The broker withholds 0,005% of every swing sale and 1% of each day's day-trade
///   profit — the "dedo-duro" — which is credited against the month's tax, and any
///   excess against later months of the same year.
/// - A DARF under R$ 10 is not issued; the amount accumulates until it reaches R$ 10.
/// - Payment is due on the last business day of the following month.
///
/// Money is rounded half-to-even at two decimals, the ABNT 5891 convention the tax
/// forms expect.
/// </summary>
public static class CapitalGains
{
    public const decimal StockExemptionCeiling = 20_000m;
    public const decimal CryptoExemptionCeiling = 35_000m;
    public const decimal DarfMinimum = 10m;
    public const decimal SwingWithholdingRate = 0.00005m;
    public const decimal DayTradeWithholdingRate = 0.01m;

    private static readonly TaxBucket[] AllBuckets = Enum.GetValues<TaxBucket>();

    public static IReadOnlyList<MonthlyTax> Compute(
        IEnumerable<Trade> trades,
        IEnumerable<CorporateAction> actions)
    {
        var taxable = trades
            .Where(t => t.Class != AssetClass.FixedIncome)
            .OrderBy(t => t.Date).ThenBy(t => t.Id)
            .ToList();

        if (taxable.Count == 0)
            return [];

        var actionsByDate = actions
            .GroupBy(a => a.Date)
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.Id).ToList());

        var tradesByDate = taxable
            .GroupBy(t => t.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var books = new Dictionary<string, Book>(StringComparer.OrdinalIgnoreCase);
        var months = new SortedDictionary<(int Year, int Month), MonthAccumulator>();

        // Walk the calendar one day at a time, over every day that has either a trade
        // or an event — a split on a quiet day still has to reach the book before the
        // next sale. Events land first, then each ticker's trades of the day are netted
        // for day-trade quantity before what is left reaches the book.
        var days = tradesByDate.Keys.Union(actionsByDate.Keys).OrderBy(d => d);

        foreach (var date in days)
        {
            if (actionsByDate.TryGetValue(date, out var todaysActions))
                foreach (var a in todaysActions)
                    if (books.TryGetValue(a.Ticker, out var held))
                        held.Apply(a);

            if (!tradesByDate.TryGetValue(date, out var todaysTrades))
                continue;

            var key = (date.Year, date.Month);
            if (!months.TryGetValue(key, out var month))
                months[key] = month = new MonthAccumulator();

            foreach (var ticker in todaysTrades.GroupBy(t => t.Ticker, StringComparer.OrdinalIgnoreCase))
            {
                if (!books.TryGetValue(ticker.Key, out var book))
                    books[ticker.Key] = book = new Book(ticker.Key, ticker.First().Class);

                SettleDay(ticker.ToList(), book, month);
            }
        }

        return Summarize(months);
    }

    /// <summary>
    /// Nets one ticker's buys and sells of one day. The matched quantity is day trade;
    /// the surplus on either side goes through the book as an ordinary buy or sell.
    /// Fees are split across the two in proportion to quantity.
    /// </summary>
    private static void SettleDay(List<Trade> trades, Book book, MonthAccumulator month)
    {
        var buys = trades.Where(t => t.Side == TradeSide.Buy).ToList();
        var sells = trades.Where(t => t.Side == TradeSide.Sell).ToList();

        var bought = buys.Sum(t => t.Quantity);
        var sold = sells.Sum(t => t.Quantity);
        var matched = Math.Min(bought, sold);

        var assetClass = trades[0].Class;
        var date = trades[0].Date;
        var userId = trades[0].UserId;

        if (matched > 0)
        {
            // Weighted prices of the day's two sides, fees folded in pro rata.
            var buyCost = buys.Sum(t => t.Gross + t.Fees) * matched / bought;
            var sellGross = sells.Sum(t => t.Gross) * matched / sold;
            var sellProceeds = sells.Sum(t => t.Gross - t.Fees) * matched / sold;

            var result = sellProceeds - buyCost;
            var bucket = BucketFor(assetClass, dayTrade: true);
            month.Add(bucket, sales: sellGross, result);

            if (bucket == TaxBucket.DayTrade && result > 0)
                month.DayTradeWithheld += result * DayTradeWithholdingRate;
        }

        // Only one side can have a surplus. Buys first, so a sale that exceeds the
        // position comes out of what was held plus what was bought today.
        var surplusBuy = bought - matched;
        if (surplusBuy > 0)
        {
            var cost = buys.Sum(t => t.Gross + t.Fees) * surplusBuy / bought;
            book.Apply(new Trade(book.Ticker, assetClass, TradeSide.Buy, surplusBuy, cost / surplusBuy, 0m, date, userId));
        }

        var surplusSell = sold - matched;
        if (surplusSell > 0)
        {
            var gross = sells.Sum(t => t.Gross) * surplusSell / sold;
            var fees = sells.Sum(t => t.Fees) * surplusSell / sold;

            var before = book.RealizedPnL;
            book.Apply(new Trade(book.Ticker, assetClass, TradeSide.Sell, surplusSell, gross / surplusSell, fees, date, userId));
            var result = book.RealizedPnL - before;

            var bucket = BucketFor(assetClass, dayTrade: false);
            month.Add(bucket, sales: gross, result);

            // Stock gains are only exempt when the month's stock sales stay under the
            // ceiling, which is not known until the month ends — tracked apart so the
            // summary can pull them out of the base if the month qualifies.
            if (assetClass == AssetClass.Stock)
            {
                month.StockSales += gross;
                if (result > 0) month.StockGains += result;
            }
            else if (assetClass == AssetClass.Crypto)
            {
                month.CryptoSales += gross;
                if (result > 0) month.CryptoGains += result;
            }

            if (bucket is TaxBucket.Common or TaxBucket.RealEstateFund)
                month.SwingWithheld += gross * SwingWithholdingRate;
        }
    }

    private static TaxBucket BucketFor(AssetClass assetClass, bool dayTrade) => assetClass switch
    {
        AssetClass.Fii => TaxBucket.RealEstateFund,
        AssetClass.Crypto => TaxBucket.Crypto,
        _ => dayTrade ? TaxBucket.DayTrade : TaxBucket.Common
    };

    private static List<MonthlyTax> Summarize(SortedDictionary<(int Year, int Month), MonthAccumulator> months)
    {
        var carriedLoss = AllBuckets.ToDictionary(b => b, _ => 0m);
        var withheldCredit = 0m;
        var creditYear = 0;
        var deferredDarf = 0m;

        var report = new List<MonthlyTax>();

        foreach (var ((year, month), acc) in months)
        {
            // A month of buying alone has nothing to declare and would only pad the table.
            if (!acc.HasSales)
                continue;

            // Withholding credit is only good within the calendar year it was withheld.
            if (year != creditYear)
            {
                withheldCredit = 0m;
                creditYear = year;
            }

            var stockExempt = acc.StockSales <= StockExemptionCeiling;
            var cryptoExempt = acc.CryptoSales <= CryptoExemptionCeiling;

            var buckets = new List<BucketResult>();
            var taxDue = 0m;

            foreach (var bucket in AllBuckets)
            {
                var (sales, result) = acc.Get(bucket);
                if (sales == 0m && carriedLoss[bucket] == 0m)
                    continue;

                var exemptGain = bucket switch
                {
                    TaxBucket.Common when stockExempt => acc.StockGains,
                    TaxBucket.Crypto when cryptoExempt => acc.CryptoGains,
                    _ => 0m
                };

                // Exempt gains leave the base; the rest of the month's result stays,
                // including any loss, which is why an exempt month can still add to the
                // loss carried forward.
                var baseBeforeLoss = result - exemptGain;
                var lossOffset = 0m;

                if (baseBeforeLoss > 0 && carriedLoss[bucket] > 0)
                {
                    lossOffset = Math.Min(baseBeforeLoss, carriedLoss[bucket]);
                    carriedLoss[bucket] -= lossOffset;
                }
                else if (baseBeforeLoss < 0)
                {
                    carriedLoss[bucket] += -baseBeforeLoss;
                }

                var taxableBase = Math.Max(baseBeforeLoss - lossOffset, 0m);
                var tax = Round(taxableBase * bucket.Rate());
                taxDue += tax;

                buckets.Add(new BucketResult(
                    bucket,
                    Round(sales),
                    Round(result),
                    Round(exemptGain),
                    Round(lossOffset),
                    Round(taxableBase),
                    tax,
                    Round(carriedLoss[bucket])));
            }

            // The broker only withholds the 0,005% once the month's total tops R$ 1;
            // the 1% on day trade is withheld regardless.
            var swingWithheld = acc.SwingWithheld;
            if (swingWithheld <= 1m)
                swingWithheld = 0m;
            var withheld = Round(swingWithheld + acc.DayTradeWithheld);

            withheldCredit += withheld;
            var creditUsed = Math.Min(taxDue, withheldCredit);
            withheldCredit -= creditUsed;

            var netTax = taxDue - creditUsed;
            var darfCandidate = netTax + deferredDarf;
            var deferred = darfCandidate > 0 && darfCandidate < DarfMinimum;
            var darfAmount = deferred ? 0m : darfCandidate;
            var deferredIn = deferredDarf;
            deferredDarf = deferred ? darfCandidate : 0m;

            report.Add(new MonthlyTax(
                year, month,
                Round(acc.StockSales), stockExempt,
                Round(acc.CryptoSales), cryptoExempt,
                buckets,
                withheld, Round(creditUsed),
                taxDue,
                Round(darfAmount), Round(deferredIn), deferred,
                DarfDueDate(year, month)));
        }

        return report;
    }

    /// <summary>Last business day of the month after — weekends only; national holidays would need a calendar.</summary>
    public static DateOnly DarfDueDate(int year, int month)
    {
        var following = new DateOnly(year, month, 1).AddMonths(1);
        var last = following.AddMonths(1).AddDays(-1);

        while (last.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            last = last.AddDays(-1);

        return last;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.ToEven);

    private sealed class MonthAccumulator
    {
        private readonly Dictionary<TaxBucket, (decimal Sales, decimal Result)> _buckets = new();

        public decimal StockSales;
        public decimal StockGains;
        public decimal CryptoSales;
        public decimal CryptoGains;
        public decimal SwingWithheld;
        public decimal DayTradeWithheld;

        public void Add(TaxBucket bucket, decimal sales, decimal result)
        {
            var current = _buckets.GetValueOrDefault(bucket);
            _buckets[bucket] = (current.Sales + sales, current.Result + result);
        }

        public (decimal Sales, decimal Result) Get(TaxBucket bucket) => _buckets.GetValueOrDefault(bucket);

        public bool HasSales => _buckets.Values.Any(b => b.Sales > 0);
    }
}
