using TrustFinance.Domain.Entities;

namespace TrustFinance.Domain.Planning;

/// <summary>How one category is tracking against its ceiling this month.</summary>
public sealed record BudgetStatus(int CategoryId, string Category, decimal Limit, decimal Spent)
{
    public decimal Remaining => Limit - Spent;
    public decimal Percent => Limit > 0 ? Spent / Limit * 100m : 0m;

    public bool IsOver => Spent > Limit;

    /// <summary>Close enough that saying so is still useful — past it, a warning is too late.</summary>
    public bool IsNear => !IsOver && Percent >= 80m;
}

/// <summary>Progress towards a single number, the shape every goal on the planning screen takes.</summary>
public sealed record GoalProgress(string Label, decimal Current, decimal Target)
{
    public bool HasTarget => Target > 0;
    public decimal Percent => Target > 0 ? Math.Min(Current / Target * 100m, 100m) : 0m;
    public decimal Remaining => Math.Max(Target - Current, 0m);
    public bool IsMet => Target > 0 && Current >= Target;
}

public static class Planning
{
    /// <summary>
    /// Budget consumption for the month containing <paramref name="today"/>. Only expenses
    /// count: a salary landing in a category would otherwise read as negative spending and
    /// quietly buy back room the user has already used.
    /// </summary>
    /// <param name="categoryNames">
    /// Id to name. Passed as data rather than read off <c>Budget.Category</c> so this
    /// stays a pure function over values — it cannot then depend on whether the caller
    /// remembered an Include.
    /// </param>
    public static IReadOnlyList<BudgetStatus> Budgets(
        IEnumerable<Budget> budgets,
        IReadOnlyDictionary<int, string> categoryNames,
        IEnumerable<Transaction> transactions,
        DateOnly today)
    {
        // Paying a card statement is a transfer, not spending; the purchases on the
        // card were the spending, and they were counted on their own dates.
        var spentByCategory = transactions
            .Where(t => t.Type == TransactionType.Expense
                        && !t.IsTransfer
                        && t.Date.Year == today.Year
                        && t.Date.Month == today.Month)
            .GroupBy(t => t.CategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

        return [.. budgets
            .Where(b => b.MonthlyLimit > 0)
            .Select(b => new BudgetStatus(
                b.CategoryId,
                categoryNames.GetValueOrDefault(b.CategoryId, string.Empty),
                b.MonthlyLimit,
                spentByCategory.GetValueOrDefault(b.CategoryId)))
            .OrderByDescending(s => s.Percent)];
    }

    /// <summary>What was put into investments during the month containing <paramref name="today"/>, net of sales.</summary>
    public static decimal ContributedThisMonth(IEnumerable<Trade> trades, DateOnly today)
    {
        var inMonth = trades.Where(t => t.Date.Year == today.Year && t.Date.Month == today.Month).ToList();

        var bought = inMonth.Where(t => t.Side == TradeSide.Buy).Sum(t => t.Gross + t.Fees);
        var sold = inMonth.Where(t => t.Side == TradeSide.Sell).Sum(t => t.Gross - t.Fees);

        return Math.Max(bought - sold, 0m);
    }
}
