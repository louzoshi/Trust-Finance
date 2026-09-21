using TrustFinance.Domain.Entities;

namespace TrustFinance.Domain.Finance;

/// <summary>
/// The dates a recurrence falls on. Every occurrence is computed from the anchor date
/// rather than from the previous occurrence, which is what keeps a monthly rent set on
/// the 31st from drifting: Jan 31 → Feb 28 → Mar 31, not Jan 31 → Feb 28 → Mar 28.
/// </summary>
public static class Schedule
{
    /// <summary>The <paramref name="n"/>th occurrence after <paramref name="anchor"/>; n = 0 is the anchor itself.</summary>
    public static DateOnly Occurrence(DateOnly anchor, Frequency frequency, int n) => frequency switch
    {
        Frequency.Weekly => anchor.AddDays(7 * n),
        Frequency.Monthly => anchor.AddMonths(n),
        Frequency.Yearly => anchor.AddYears(n),
        _ => throw new ArgumentOutOfRangeException(nameof(frequency))
    };

    /// <summary>The first occurrence on or after <paramref name="date"/>, never earlier than the anchor.</summary>
    public static DateOnly NextOnOrAfter(DateOnly anchor, Frequency frequency, DateOnly date)
    {
        if (date <= anchor)
            return anchor;

        // Start from a lower-bound guess and walk forward; the guess is exact or one
        // short for every frequency, so the loop runs at most a couple of times.
        var n = frequency switch
        {
            Frequency.Weekly => (date.DayNumber - anchor.DayNumber) / 7,
            Frequency.Monthly => (date.Year - anchor.Year) * 12 + date.Month - anchor.Month - 1,
            Frequency.Yearly => date.Year - anchor.Year - 1,
            _ => throw new ArgumentOutOfRangeException(nameof(frequency))
        };
        n = Math.Max(n, 0);

        while (Occurrence(anchor, frequency, n) < date)
            n++;

        return Occurrence(anchor, frequency, n);
    }

    /// <summary>The first occurrence strictly after <paramref name="date"/>.</summary>
    public static DateOnly NextAfter(DateOnly anchor, Frequency frequency, DateOnly date)
        => NextOnOrAfter(anchor, frequency, date.AddDays(1));
}
