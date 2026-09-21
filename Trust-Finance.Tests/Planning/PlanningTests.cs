using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Planning;

namespace TrustFinance.Tests.Planning;

public class PlanningTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);

    private const int Someone = 1;

    private static Transaction Expense(int categoryId, decimal amount, string date = "2026-09-05") =>
        new("Compra", amount, DateOnly.Parse(date), TransactionType.Expense, categoryId, Someone);

    private static Budget Limit(int categoryId, decimal limit) => new(categoryId, limit, Someone);

    private static Dictionary<int, string> Named(int categoryId, string name) =>
        new() { [categoryId] = name };

    private static Trade Trade(TradeSide side, decimal qty, decimal price, decimal fees, DateOnly date) =>
        new("PETR4", AssetClass.Stock, side, qty, price, fees, date, Someone);

    [Fact]
    public void Budget_Should_Only_Count_This_Months_Expenses()
    {
        var budgets = new[] { Limit(1, 1000m) };
        var transactions = new[]
        {
            Expense(1, 400m, "2026-09-03"),
            Expense(1, 250m, "2026-09-18"),
            Expense(1, 900m, "2026-08-30")
        };

        var status = global::TrustFinance.Domain.Planning.Planning.Budgets(budgets, Named(1, "Mercado"), transactions, Today).Single();

        status.Spent.Should().Be(650m);
        status.Remaining.Should().Be(350m);
        status.IsOver.Should().BeFalse();
    }

    [Fact]
    public void Income_Should_Not_Buy_Back_Budget_Room()
    {
        var budgets = new[] { Limit(1, 500m) };
        var transactions = new[]
        {
            Expense(1, 480m),
            new Transaction("Salário", 2000m, Today, TransactionType.Income, 1, Someone)
        };

        global::TrustFinance.Domain.Planning.Planning.Budgets(budgets, Named(1, "Mercado"), transactions, Today).Single().Spent.Should().Be(480m);
    }

    [Theory]
    [InlineData(790, false, false)]
    [InlineData(800, false, true)]
    [InlineData(1001, true, false)]
    public void Budget_Should_Flag_Near_And_Over(decimal spent, bool over, bool near)
    {
        var status = new BudgetStatus(1, "Mercado", 1000m, spent);

        status.IsOver.Should().Be(over);
        status.IsNear.Should().Be(near);
    }

    [Fact]
    public void Contribution_Should_Net_Sales_Against_Purchases()
    {
        var trades = new[]
        {
            Trade(TradeSide.Buy, 100, 10m, 5m, new(2026, 9, 4)),
            Trade(TradeSide.Sell, 20, 10m, 5m, new(2026, 9, 10)),
            Trade(TradeSide.Buy, 50, 10m, 0m, new(2026, 8, 4))
        };

        // 1005 in, 195 out, and August is a different month.
        global::TrustFinance.Domain.Planning.Planning.ContributedThisMonth(trades, Today).Should().Be(810m);
    }

    [Fact]
    public void Contribution_Should_Never_Go_Negative()
    {
        var trades = new[]
        {
            Trade(TradeSide.Sell, 100, 10m, 0m, new(2026, 9, 4))
        };

        global::TrustFinance.Domain.Planning.Planning.ContributedThisMonth(trades, Today).Should().Be(0m);
    }
}

public class NoticesTests
{
    private static readonly DateTime Now = new(2026, 9, 21, 10, 0, 0, DateTimeKind.Local);

    private static PriceAlert Armed(string ticker, AlertDirection direction, decimal target) =>
        new(ticker, AssetClass.Stock, direction, target, userId: 1);

    private static PriceAlert Triggered(string ticker, AlertDirection direction, decimal target, decimal at)
    {
        var alert = Armed(ticker, direction, target);
        alert.Trigger(at, Now);
        return alert;
    }

    private static PriceAlert Dismissed(string ticker, AlertDirection direction, decimal target, decimal at)
    {
        var alert = Triggered(ticker, direction, target, at);
        alert.Dismiss();
        return alert;
    }

    private static UserSettings AllOn => new()
    {
        NotifyPriceAlerts = true,
        NotifyBudgetOverruns = true,
        NotifyContributionGoal = true
    };

    [Fact]
    public void A_Triggered_Alert_Should_Produce_A_Notice()
    {
        var alerts = new[]
        {
            Triggered("PETR4", AlertDirection.Above, target: 38m, at: 38.4m)
        };

        var notices = Notices.Build(alerts, [], new GoalProgress("x", 0, 0), AllOn, Now);

        notices.Should().ContainSingle();
        notices[0].Subject.Should().Be("PETR4");
        notices[0].Kind.Should().Be(NoticeKind.PriceAlertAbove);
        notices[0].Value.Should().Be(38.4m);
        notices[0].Reference.Should().Be(38m);
    }

    [Fact]
    public void An_Armed_Or_Dismissed_Alert_Should_Stay_Quiet()
    {
        var alerts = new[]
        {
            Armed("VALE3", AlertDirection.Below, 60m),
            Dismissed("ITUB4", AlertDirection.Below, target: 30m, at: 29m)
        };

        Notices.Build(alerts, [], new GoalProgress("x", 0, 0), AllOn, Now).Should().BeEmpty();
    }

    [Fact]
    public void Switching_A_Source_Off_Should_Silence_It()
    {
        var budgets = new[] { new BudgetStatus(1, "Mercado", 100m, 200m) };
        var settings = AllOn;
        settings.NotifyBudgetOverruns = false;

        Notices.Build([], budgets, new GoalProgress("x", 0, 0), settings, Now).Should().BeEmpty();
    }

    [Fact]
    public void An_Overrun_Should_Outrank_An_Informational_Notice()
    {
        var budgets = new[]
        {
            new BudgetStatus(1, "Mercado", 100m, 200m),
            new BudgetStatus(2, "Lazer", 100m, 85m)
        };

        var notices = Notices.Build([], budgets, new GoalProgress("x", 0, 0), AllOn, Now);

        notices.Should().HaveCount(2);
        notices[0].Level.Should().Be(NoticeLevel.Warning);
        notices[0].Subject.Should().Be("Mercado");
    }

    [Fact]
    public void The_Contribution_Nudge_Should_Wait_Until_The_End_Of_The_Month()
    {
        var goal = new GoalProgress("Aporte", 200m, 1000m);
        var early = new DateTime(2026, 9, 5, 10, 0, 0, DateTimeKind.Local);

        Notices.Build([], [], goal, AllOn, early).Should().BeEmpty();
        Notices.Build([], [], goal, AllOn, Now).Should().ContainSingle();
    }

    [Fact]
    public void A_Met_Contribution_Goal_Should_Not_Nag()
    {
        var goal = new GoalProgress("Aporte", 1200m, 1000m);

        Notices.Build([], [], goal, AllOn, Now).Should().BeEmpty();
    }
}
