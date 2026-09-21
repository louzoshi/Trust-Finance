namespace TrustFinance.App;

/// <summary>
/// A closed date range for filtering the ledger. Either end may be open. The presets
/// are the ones a person actually reaches for; anything else is a custom range.
/// </summary>
public readonly record struct Period(DateOnly? From, DateOnly? To)
{
    public static readonly Period All = new(null, null);

    public static Period ThisMonth(DateOnly today)
    {
        var first = new DateOnly(today.Year, today.Month, 1);
        return new(first, first.AddMonths(1).AddDays(-1));
    }

    public static Period LastMonth(DateOnly today)
    {
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        return new(first, first.AddMonths(1).AddDays(-1));
    }

    /// <summary>This month and the two before it, from the 1st of the earliest.</summary>
    public static Period LastThreeMonths(DateOnly today)
        => new(new DateOnly(today.Year, today.Month, 1).AddMonths(-2), ThisMonth(today).To);

    public static Period ThisYear(DateOnly today)
        => new(new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31));

    public bool Contains(DateOnly date)
        => (From is null || date >= From) && (To is null || date <= To);
}
