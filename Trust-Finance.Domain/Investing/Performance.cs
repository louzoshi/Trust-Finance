using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Markets;

namespace TrustFinance.Domain.Investing;

/// <summary>Money moving between the investor and the portfolio. Negative goes in, positive comes out.</summary>
public sealed record CashFlow(DateOnly Date, decimal Amount);

/// <summary>A point on the "what if it had been CDI" chart.</summary>
public sealed record PerformancePoint(DateOnly Date, decimal NetContributions, decimal CdiValue);

public sealed record PerformanceSummary(
    /// <summary>Money-weighted annual return of the whole portfolio, as a percentage.</summary>
    decimal? AnnualReturn,
    /// <summary>CDI over the same span, annualized, as a percentage.</summary>
    decimal? CdiAnnual,
    /// <summary>What the same deposits and withdrawals would be worth today had they sat at CDI.</summary>
    decimal CdiValue,
    /// <summary>What the portfolio is actually worth today.</summary>
    decimal MarketValue,
    decimal NetContributions,
    IReadOnlyList<PerformancePoint> Series,
    DateOnly From,
    DateOnly To)
{
    /// <summary>"120% do CDI": the portfolio's return over the benchmark's, or null when either is missing.</summary>
    public decimal? PercentOfCdi =>
        AnnualReturn is { } r && CdiAnnual is > 0 and { } c ? r / c * 100m : null;

    public decimal GainOverCdi => MarketValue - CdiValue;
}

/// <summary>
/// How the portfolio has done, measured the way a bank statement measures it: against
/// the CDI, over the exact days the money was invested.
///
/// The return is money-weighted (an internal rate of return over the dated cash
/// flows) rather than time-weighted, because there is no price history to build a
/// daily curve from — and because for one person's own money, "what did my deposits
/// earn" is the question. The CDI figure is computed by pushing the same cash flows
/// through the actual daily series, so both sides of the comparison saw the same
/// dates and the same amounts.
/// </summary>
public static class Performance
{
    /// <summary>
    /// Every buy is money in, every sale and payout money out; fees and withholding are
    /// already inside those amounts. Fixed income is included: it is invested money too.
    /// </summary>
    public static List<CashFlow> CashFlows(IEnumerable<Trade> trades, IEnumerable<Payout> payouts)
    {
        var flows = trades
            .Select(t => new CashFlow(t.Date, t.Side == TradeSide.Buy ? -t.NetCashFlow : t.NetCashFlow))
            .Concat(payouts.Select(p => new CashFlow(p.PaymentDate, p.NetAmount)))
            .Where(f => f.Amount != 0m)
            .OrderBy(f => f.Date)
            .ToList();

        return flows;
    }

    public static PerformanceSummary Summarize(
        IReadOnlyList<CashFlow> flows,
        decimal marketValue,
        IReadOnlyList<DailyRate> cdi,
        DateOnly asOf)
    {
        if (flows.Count == 0)
            return new PerformanceSummary(null, null, 0m, marketValue, 0m, [], asOf, asOf);

        var from = flows[0].Date;

        // Close the position out at today's value so the IRR has an end to solve for.
        var closed = flows.Append(new CashFlow(asOf, marketValue)).ToList();
        var irr = Xirr(closed);

        var cdiValue = CompoundAtCdi(flows, cdi, asOf);
        var cdiAnnual = AnnualizedCdi(cdi, from, asOf);

        var series = MonthlySeries(flows, cdi, from, asOf);

        return new PerformanceSummary(
            irr is { } r ? Math.Round(r * 100m, 2) : null,
            cdiAnnual is { } c ? Math.Round(c * 100m, 2) : null,
            Math.Round(cdiValue, 2),
            marketValue,
            Math.Round(-flows.Sum(f => f.Amount), 2),
            series,
            from,
            asOf);
    }

    /// <summary>
    /// Annualized internal rate of return over irregularly dated flows, by Newton's
    /// method with a bisection fallback. Null when the flows cannot have a rate: all one
    /// sign, or nothing invested for any length of time.
    /// </summary>
    public static decimal? Xirr(IReadOnlyList<CashFlow> flows)
    {
        if (flows.Count < 2 || flows.All(f => f.Amount >= 0) || flows.All(f => f.Amount <= 0))
            return null;

        var t0 = flows.Min(f => f.Date);
        var span = flows.Max(f => f.Date).DayNumber - t0.DayNumber;
        if (span == 0)
            return null;

        var dated = flows
            .Select(f => (Years: (f.Date.DayNumber - t0.DayNumber) / 365.0, Amount: (double)f.Amount))
            .ToList();

        double Npv(double rate) => dated.Sum(f => f.Amount / Math.Pow(1 + rate, f.Years));
        double Derivative(double rate) => dated.Sum(f => -f.Years * f.Amount / Math.Pow(1 + rate, f.Years + 1));

        var guess = 0.1;
        for (var i = 0; i < 50; i++)
        {
            var value = Npv(guess);
            var slope = Derivative(guess);
            if (Math.Abs(slope) < 1e-12) break;

            var next = guess - value / slope;
            if (next <= -0.9999) break;
            if (Math.Abs(next - guess) < 1e-9)
                return Finite(next);
            guess = next;
        }

        // Bisection over a wide but sane bracket: −99% to +1000% a year.
        double lo = -0.99, hi = 10.0;
        if (Math.Sign(Npv(lo)) == Math.Sign(Npv(hi)))
            return null;

        for (var i = 0; i < 200; i++)
        {
            var mid = (lo + hi) / 2;
            if (Math.Sign(Npv(mid)) == Math.Sign(Npv(lo))) lo = mid; else hi = mid;
            if (hi - lo < 1e-9) break;
        }

        return Finite((lo + hi) / 2);

        static decimal? Finite(double rate)
            => double.IsFinite(rate) && Math.Abs(rate) < 1e6 ? (decimal)rate : null;
    }

    /// <summary>Each flow compounded at the daily CDI from its own date to <paramref name="asOf"/>, summed.</summary>
    public static decimal CompoundAtCdi(IEnumerable<CashFlow> flows, IReadOnlyList<DailyRate> cdi, DateOnly asOf)
    {
        // Prefix products let every flow be valued with one division instead of a loop
        // over its own days: value(a→b) = P(b) / P(a).
        var index = PrefixIndex(cdi);
        var total = 0m;

        foreach (var f in flows)
            total += -f.Amount * Growth(index, f.Date, asOf);

        return total;
    }

    /// <summary>CDI between two dates expressed as a yearly rate, on a 252 business-day year.</summary>
    public static decimal? AnnualizedCdi(IReadOnlyList<DailyRate> cdi, DateOnly from, DateOnly to)
    {
        var days = cdi.Count(r => r.Date > from && r.Date <= to);
        if (days == 0)
            return null;

        var growth = (double)Growth(PrefixIndex(cdi), from, to);
        return (decimal)(Math.Pow(growth, 252.0 / days) - 1);
    }

    private static List<PerformancePoint> MonthlySeries(
        IReadOnlyList<CashFlow> flows, IReadOnlyList<DailyRate> cdi, DateOnly from, DateOnly asOf)
    {
        var index = PrefixIndex(cdi);
        var points = new List<PerformancePoint>();

        var cursor = new DateOnly(from.Year, from.Month, 1).AddMonths(1).AddDays(-1);
        while (cursor < asOf)
        {
            points.Add(PointAt(cursor));
            cursor = cursor.AddMonths(1);
            cursor = new DateOnly(cursor.Year, cursor.Month, 1).AddMonths(1).AddDays(-1);
        }
        points.Add(PointAt(asOf));

        return points;

        PerformancePoint PointAt(DateOnly date)
        {
            var upTo = flows.Where(f => f.Date <= date).ToList();
            var contributed = -upTo.Sum(f => f.Amount);
            var atCdi = upTo.Sum(f => -f.Amount * Growth(index, f.Date, date));
            return new PerformancePoint(date, Math.Round(contributed, 2), Math.Round(atCdi, 2));
        }
    }

    /// <summary>Cumulative growth factor at the close of every day in the series, in date order.</summary>
    private static (DateOnly[] Dates, decimal[] Factors) PrefixIndex(IReadOnlyList<DailyRate> cdi)
    {
        var ordered = cdi.OrderBy(r => r.Date).ToList();
        var dates = new DateOnly[ordered.Count];
        var factors = new decimal[ordered.Count];
        var running = 1m;

        for (var i = 0; i < ordered.Count; i++)
        {
            running *= 1 + ordered[i].Percent / 100m;
            dates[i] = ordered[i].Date;
            factors[i] = running;
        }

        return (dates, factors);
    }

    /// <summary>Growth from the close of <paramref name="from"/> to the close of <paramref name="to"/>. 1 when the series does not cover it.</summary>
    private static decimal Growth((DateOnly[] Dates, decimal[] Factors) index, DateOnly from, DateOnly to)
    {
        if (index.Dates.Length == 0 || to <= from)
            return 1m;

        var start = FactorAt(index, from);
        var end = FactorAt(index, to);
        return start > 0 ? end / start : 1m;
    }

    /// <summary>The prefix factor at the last series day on or before <paramref name="date"/>; 1 before the series starts.</summary>
    private static decimal FactorAt((DateOnly[] Dates, decimal[] Factors) index, DateOnly date)
    {
        var position = Array.BinarySearch(index.Dates, date);
        if (position < 0)
            position = ~position - 1;

        return position < 0 ? 1m : index.Factors[position];
    }
}
