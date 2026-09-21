using TrustFinance.Domain.Entities;

namespace TrustFinance.Domain.Finance;

/// <summary>Income against expense for one calendar month.</summary>
public sealed record MonthFlow(int Year, int Month, decimal Income, decimal Expense)
{
    public decimal Balance => Income - Expense;
    public bool HadActivity => Income > 0 || Expense > 0;
    public DateOnly FirstDay => new(Year, Month, 1);
}

public sealed record CategorySpend(string Category, decimal Total);

/// <summary>The two names the summary needs but cannot invent, passed in by the app layer.</summary>
public sealed record CategoryLabels(string Other, string Uncategorized);

public sealed record DashboardSummary(
    MonthFlow Current,
    /// <summary>Last month's balance, or null when there was nothing to compare against.</summary>
    decimal? PreviousBalance,
    string? TopCategory,
    IReadOnlyList<MonthFlow> ByMonth,
    IReadOnlyList<CategorySpend> ByCategory,
    IReadOnlyList<Transaction> Recent);

public static class MonthlyFlow
{
    public const int MonthsShown = 6;
    public const int TopCategories = 6;
    public const int RecentShown = 5;

    /// <param name="labels">
    /// What to call the bucket of everything past the top few, and a category whose name
    /// is missing. Supplied by the caller: the domain has no language.
    /// </param>
    public static DashboardSummary Summarize(
        IReadOnlyCollection<Transaction> transactions,
        IReadOnlyDictionary<int, string> categoryNames,
        DateOnly today,
        CategoryLabels labels)
    {
        var months = LastMonths(today, MonthsShown);
        var currentKey = months[^1];
        var previousKey = months[^2];

        // Income and expense are tracked apart all the way through: summing them into
        // one number is what made an earlier dashboard report volume instead of a balance.
        var flow = months.ToDictionary(m => m, _ => (income: 0m, expense: 0m));

        // Transfers move money between the user's own accounts; neither half is income
        // or expense. Card purchases are spending on the day they happened, and the
        // statement payment that follows is a transfer.
        foreach (var t in transactions.Where(t => !t.IsTransfer))
        {
            var key = new DateOnly(t.Date.Year, t.Date.Month, 1);
            if (!flow.TryGetValue(key, out var f)) continue;
            flow[key] = t.Type == TransactionType.Income
                ? (f.income + t.Amount, f.expense)
                : (f.income, f.expense + t.Amount);
        }

        var byMonth = months
            .Select(m => new MonthFlow(m.Year, m.Month, flow[m].income, flow[m].expense))
            .ToList();

        var current = byMonth[^1];
        var previous = byMonth[^2];

        // Only expenses break down by category. A ranking that mixed a salary in with
        // the grocery bill would put income at the top and say nothing about spending.
        var ranked = transactions
            .Where(t => t.Type == TransactionType.Expense
                        && !t.IsTransfer
                        && t.Date.Year == currentKey.Year
                        && t.Date.Month == currentKey.Month)
            .GroupBy(t => categoryNames.TryGetValue(t.CategoryId, out var name) ? name : labels.Uncategorized)
            .Select(g => new CategorySpend(g.Key, g.Sum(t => t.Amount)))
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.Category, StringComparer.CurrentCulture)
            .ToList();

        var byCategory = ranked.Take(TopCategories).ToList();
        var tail = ranked.Skip(TopCategories).Sum(c => c.Total);
        if (tail > 0)
            byCategory.Add(new CategorySpend(labels.Other, tail));

        var recent = transactions
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .Take(RecentShown)
            .ToList();

        return new DashboardSummary(
            Current: current,
            PreviousBalance: previous.HadActivity ? previous.Balance : null,
            TopCategory: ranked.FirstOrDefault()?.Category,
            ByMonth: byMonth,
            ByCategory: byCategory,
            Recent: recent);
    }

    /// <summary>The first day of each of the last <paramref name="count"/> months, oldest first, ending in today's.</summary>
    public static IReadOnlyList<DateOnly> LastMonths(DateOnly today, int count)
    {
        var first = new DateOnly(today.Year, today.Month, 1);
        return Enumerable.Range(0, count)
            .Select(i => first.AddMonths(i - (count - 1)))
            .ToList();
    }
}
