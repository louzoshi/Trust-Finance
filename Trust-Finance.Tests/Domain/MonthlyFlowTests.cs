using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Finance;

namespace TrustFinance.Tests.Domain;

public class MonthlyFlowTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);
    private static readonly Dictionary<int, string> Categories = new() { [1] = "Mercado", [2] = "Lazer", [3] = "Salário" };

    private const int Someone = 1;
    private const int Wallet = 9;
    private static readonly CategoryLabels Labels = new(Other: "Outras", Uncategorized: "Sem categoria");

    private static Transaction Tx(int id, decimal amount, TransactionType type, DateOnly date, int categoryId = 1)
        => new($"#{id}", amount, date, type, categoryId, Wallet, Someone);

    [Fact]
    public void LastMonths_Should_End_In_The_Current_Month_And_Cross_The_Year()
    {
        var months = MonthlyFlow.LastMonths(new DateOnly(2026, 2, 10), 6);

        months.Should().Equal(
            new DateOnly(2025, 9, 1), new DateOnly(2025, 10, 1), new DateOnly(2025, 11, 1),
            new DateOnly(2025, 12, 1), new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1));
    }

    [Fact]
    public void Summarize_Should_Keep_Income_And_Expense_Apart()
    {
        var transactions = new[]
        {
            Tx(1, 4000, TransactionType.Income, Today, categoryId: 3),
            Tx(2, 300, TransactionType.Expense, Today),
            Tx(3, 200, TransactionType.Expense, Today.AddDays(-5), categoryId: 2),
        };

        var summary = MonthlyFlow.Summarize(transactions, Categories, Today, Labels);

        summary.Current.Income.Should().Be(4000);
        summary.Current.Expense.Should().Be(500);
        summary.Current.Balance.Should().Be(3500);
    }

    [Fact]
    public void Summarize_Should_Report_Last_Months_Balance_Only_When_It_Had_Activity()
    {
        var quiet = MonthlyFlow.Summarize([Tx(1, 10, TransactionType.Expense, Today)], Categories, Today, Labels);
        quiet.PreviousBalance.Should().BeNull();

        var lastMonth = Today.AddMonths(-1);
        var busy = MonthlyFlow.Summarize(
            [Tx(1, 100, TransactionType.Income, lastMonth), Tx(2, 30, TransactionType.Expense, lastMonth)],
            Categories, Today, Labels);
        busy.PreviousBalance.Should().Be(70);
    }

    [Fact]
    public void Summarize_Should_Rank_Only_This_Months_Expenses_By_Category()
    {
        var transactions = new[]
        {
            Tx(1, 9000, TransactionType.Income, Today, categoryId: 3),      // income never ranks
            Tx(2, 100, TransactionType.Expense, Today, categoryId: 1),
            Tx(3, 250, TransactionType.Expense, Today, categoryId: 2),
            Tx(4, 999, TransactionType.Expense, Today.AddMonths(-1), categoryId: 1), // last month
        };

        var summary = MonthlyFlow.Summarize(transactions, Categories, Today, Labels);

        summary.TopCategory.Should().Be("Lazer");
        summary.ByCategory.Should().Equal(new CategorySpend("Lazer", 250), new CategorySpend("Mercado", 100));
    }

    [Fact]
    public void Summarize_Should_Fold_The_Tail_Into_Other()
    {
        var names = Enumerable.Range(1, 8).ToDictionary(i => i, i => $"Cat {i}");
        var transactions = Enumerable.Range(1, 8)
            .Select(i => Tx(i, 100 * i, TransactionType.Expense, Today, categoryId: i))
            .ToArray();

        var summary = MonthlyFlow.Summarize(transactions, names, Today, Labels);

        summary.ByCategory.Should().HaveCount(MonthlyFlow.TopCategories + 1);
        summary.ByCategory[^1].Should().Be(new CategorySpend(Labels.Other, 100 + 200));
    }

    [Fact]
    public void Summarize_Should_Ignore_Transactions_Older_Than_The_Window()
    {
        var old = Tx(1, 500, TransactionType.Expense, Today.AddMonths(-MonthlyFlow.MonthsShown));

        var summary = MonthlyFlow.Summarize([old], Categories, Today, Labels);

        summary.ByMonth.Should().HaveCount(MonthlyFlow.MonthsShown);
        summary.ByMonth.Should().OnlyContain(m => !m.HadActivity);
    }

    [Fact]
    public void Summarize_Should_List_The_Most_Recent_Five()
    {
        var transactions = Enumerable.Range(1, 7)
            .Select(i => Tx(i, 1, TransactionType.Expense, Today.AddDays(-i)))
            .ToArray();

        var summary = MonthlyFlow.Summarize(transactions, Categories, Today, Labels);

        summary.Recent.Select(t => t.Date).Should().BeInDescendingOrder();
        summary.Recent.Select(t => t.Description).Should().Equal("#1", "#2", "#3", "#4", "#5");
    }
}
