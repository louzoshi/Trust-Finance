namespace TrustFinance.Domain.Markets;

/// <summary>What the gain on a fixed-income paper is worth after the government.</summary>
public readonly record struct TaxBreakdown(decimal Gross, decimal Iof, decimal IncomeTax, decimal IncomeTaxRate)
{
    /// <summary>The gain that actually reaches the account.</summary>
    public decimal Net => Gross - Iof - IncomeTax;

    public decimal Total => Iof + IncomeTax;
}

/// <summary>
/// The two taxes on fixed income, both charged on the gain and both withheld by the
/// payer — so a headline rate of "13% ao ano" is never what the holder receives.
///
/// Income tax follows the regressive table of IN RFB 1585/2015, by calendar days held:
/// 22,5% up to 180 days, 20% to 360, 17,5% to 720, and 15% after that. The table is
/// the reason "CDB de 2 anos" and "CDB de 1 ano" at the same rate are different papers.
///
/// IOF is charged on top for the first 29 days, on a table that starts at 96% of the
/// gain and reaches zero on the thirtieth day — which is what makes very short paper
/// pointless. It is computed before income tax and reduces the base income tax sees.
///
/// LCI, LCA, CRI, CRA, incentivised debentures and poupança are exempt for an
/// individual, which is why a 90% CDI LCI can beat a 105% CDI CDB.
/// </summary>
public static class FixedIncomeTax
{
    /// <summary>Percentage of the gain taken by IOF on each of the first 30 days.</summary>
    private static readonly int[] IofTable =
    [
        96, 93, 90, 86, 83, 80, 76, 73, 70, 66,
        63, 60, 56, 53, 50, 46, 43, 40, 36, 33,
        30, 26, 23, 20, 16, 13, 10,  6,  3,  0
    ];

    public static decimal IncomeTaxRate(int calendarDays) => calendarDays switch
    {
        <= 180 => 0.225m,
        <= 360 => 0.20m,
        <= 720 => 0.175m,
        _ => 0.15m
    };

    /// <summary>The share of the gain IOF takes after <paramref name="calendarDays"/>; nothing from the thirtieth day on.</summary>
    public static decimal IofRate(int calendarDays)
    {
        if (calendarDays <= 0)
            return 0.96m;

        return calendarDays >= IofTable.Length ? 0m : IofTable[calendarDays - 1] / 100m;
    }

    /// <summary>
    /// Splits a gross gain into what is withheld and what is left.
    /// </summary>
    /// <param name="exempt">LCI, LCA, CRI, CRA and friends: no income tax, and no IOF either.</param>
    public static TaxBreakdown On(decimal grossGain, int calendarDays, bool exempt)
    {
        if (grossGain <= 0 || exempt)
            return new TaxBreakdown(Math.Max(grossGain, 0m), 0m, 0m, 0m);

        var iof = Round(grossGain * IofRate(calendarDays));
        var rate = IncomeTaxRate(calendarDays);
        var tax = Round((grossGain - iof) * rate);

        return new TaxBreakdown(grossGain, iof, tax, rate);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.ToEven);
}
