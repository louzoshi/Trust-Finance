namespace TrustFinance.Domain.Markets;

/// <summary>
/// The arithmetic of a Brazilian interest rate. Everything is "ao ano, base 252": a
/// rate compounds once per business day, and a year is 252 of them by definition, not
/// by counting.
///
/// Rates enter and leave as percentages, because that is how they are quoted and
/// stored; the exponentials happen in double and come back rounded, since decimal has
/// no Pow and the input precision does not justify a series expansion.
/// </summary>
public static class Rates
{
    /// <summary>The factor one business day at <paramref name="annualPercent"/> multiplies by.</summary>
    public static decimal DailyFactor(decimal annualPercent)
        => (decimal)Math.Pow(1 + (double)annualPercent / 100, 1.0 / BusinessCalendar.YearInBusinessDays);

    /// <summary>What <paramref name="annualPercent"/> grows to over <paramref name="businessDays"/> days.</summary>
    public static decimal Factor(decimal annualPercent, int businessDays)
        => businessDays <= 0 ? 1m : (decimal)Math.Pow(1 + (double)annualPercent / 100, businessDays / 252.0);

    /// <summary>The annual rate a growth <paramref name="factor"/> over <paramref name="businessDays"/> days implies.</summary>
    public static decimal AnnualFrom(decimal factor, int businessDays)
        => businessDays <= 0 || factor <= 0
            ? 0m
            : (decimal)(Math.Pow((double)factor, 252.0 / businessDays) - 1) * 100m;

    /// <summary>
    /// A daily index quoted as a percentage for that day — the shape the Banco Central
    /// publishes the CDI and the SELIC in — as the factor for one day.
    /// </summary>
    public static decimal DailyFactorFromPublished(decimal dailyPercent) => 1 + dailyPercent / 100m;

    /// <summary>
    /// The CETIP convention for a paper paying a percentage of the CDI: the day's rate
    /// is taken down to <paramref name="percentOfCdi"/> of itself before compounding,
    /// not the accumulated factor afterwards. Taking 110% of the accumulated number
    /// instead is the classic way to overstate a CDB's return.
    /// </summary>
    public static decimal PercentOfCdiFactor(IEnumerable<DailyRate> cdi, decimal percentOfCdi)
    {
        var factor = 1m;
        foreach (var day in cdi)
            factor *= 1 + day.Percent / 100m * (percentOfCdi / 100m);

        return factor;
    }

    /// <summary>An index accumulated over the days given, each quoted as that day's percentage.</summary>
    public static decimal IndexFactor(IEnumerable<DailyRate> series)
    {
        var factor = 1m;
        foreach (var day in series)
            factor *= 1 + day.Percent / 100m;

        return factor;
    }
}

/// <summary>One day of a published index, as the Banco Central's open data returns it.</summary>
public readonly record struct DailyRate(DateOnly Date, decimal Percent);
