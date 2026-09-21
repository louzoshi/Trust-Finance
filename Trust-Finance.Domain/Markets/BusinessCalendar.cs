namespace TrustFinance.Domain.Markets;

/// <summary>
/// The Brazilian financial calendar: which days count.
///
/// Everything in this country's fixed income is quoted "ao ano, base 252 dias úteis",
/// so a rate means nothing without agreeing on which days are business days. The list
/// below is ANBIMA's national holidays, the one the market actually counts with:
/// the fixed dates, the ones tied to Easter, and — from 2024 — Black Consciousness Day,
/// made a national holiday by Lei 14.759/2023.
///
/// Municipal and state holidays are deliberately absent: they do not stop the national
/// market. B3's trading calendar differs on the edges (no session on 31 December, a
/// short session on 24 December), which changes when an order can be sent but not how
/// many days a rate accrues over.
/// </summary>
public static class BusinessCalendar
{
    /// <summary>The convention every rate here is quoted in: business days in a year.</summary>
    public const int YearInBusinessDays = 252;

    private static readonly Dictionary<int, HashSet<DateOnly>> Cache = [];
    private static readonly Lock Gate = new();

    public static bool IsBusinessDay(DateOnly date)
        => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !IsHoliday(date);

    public static bool IsHoliday(DateOnly date) => HolidaysOf(date.Year).Contains(date);

    /// <summary>
    /// Business days in the half-open interval [<paramref name="from"/>, <paramref name="to"/>):
    /// the start day is counted, the end day is not. That is the market convention — a
    /// paper bought and sold the same day has accrued nothing.
    /// </summary>
    public static int Count(DateOnly from, DateOnly to)
    {
        if (to <= from)
            return 0;

        var days = 0;
        for (var d = from; d < to; d = d.AddDays(1))
            if (IsBusinessDay(d))
                days++;

        return days;
    }

    /// <summary>The same day when it is a business day, otherwise the next one.</summary>
    public static DateOnly NextOrSame(DateOnly date)
    {
        while (!IsBusinessDay(date))
            date = date.AddDays(1);

        return date;
    }

    /// <summary>The same day when it is a business day, otherwise the previous one.</summary>
    public static DateOnly PreviousOrSame(DateOnly date)
    {
        while (!IsBusinessDay(date))
            date = date.AddDays(-1);

        return date;
    }

    /// <summary><paramref name="count"/> business days after <paramref name="date"/>; negative walks back.</summary>
    public static DateOnly Add(DateOnly date, int count)
    {
        var step = count >= 0 ? 1 : -1;
        var remaining = Math.Abs(count);

        while (remaining > 0)
        {
            date = date.AddDays(step);
            if (IsBusinessDay(date))
                remaining--;
        }

        return date;
    }

    /// <summary>Every national holiday of one year, cached: the set is asked for on every accrued day.</summary>
    public static IReadOnlySet<DateOnly> HolidaysOf(int year)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(year, out var cached))
                return cached;

            var easter = Easter(year);
            var holidays = new HashSet<DateOnly>
            {
                new(year, 1, 1),            // Confraternização Universal
                easter.AddDays(-48),        // Carnaval, segunda
                easter.AddDays(-47),        // Carnaval, terça
                easter.AddDays(-2),         // Sexta-feira Santa
                new(year, 4, 21),           // Tiradentes
                new(year, 5, 1),            // Dia do Trabalho
                easter.AddDays(60),         // Corpus Christi
                new(year, 9, 7),            // Independência
                new(year, 10, 12),          // Nossa Senhora Aparecida
                new(year, 11, 2),           // Finados
                new(year, 11, 15),          // Proclamação da República
                new(year, 12, 25)           // Natal
            };

            // National only from 2024; before that it was a local holiday in some states
            // and the market traded normally.
            if (year >= 2024)
                holidays.Add(new DateOnly(year, 11, 20));   // Consciência Negra

            Cache[year] = holidays;
            return holidays;
        }
    }

    /// <summary>Easter Sunday by the Meeus/Jones/Butcher algorithm for the Gregorian calendar.</summary>
    public static DateOnly Easter(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;

        return new DateOnly(year, month, day);
    }
}
