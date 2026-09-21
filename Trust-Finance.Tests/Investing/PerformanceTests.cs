using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Investing;

namespace TrustFinance.Tests.Investing;

public class PerformanceTests
{
    private const int Someone = 1;

    /// <summary>A flat CDI of <paramref name="dailyPercent"/> on every weekday of the range.</summary>
    private static List<DailyRate> FlatCdi(DateOnly from, DateOnly to, decimal dailyPercent)
    {
        var rates = new List<DailyRate>();
        for (var d = from; d <= to; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                rates.Add(new DailyRate(d, dailyPercent));
        return rates;
    }

    [Fact]
    public void Xirr_Should_Recover_A_Known_Annual_Rate()
    {
        var flows = new[]
        {
            new CashFlow(new DateOnly(2025, 1, 1), -1000m),
            new CashFlow(new DateOnly(2026, 1, 1), 1100m)
        };

        Performance.Xirr(flows).Should().BeApproximately(0.10m, 0.0001m);
    }

    [Fact]
    public void Xirr_Should_Handle_Several_Deposits()
    {
        // Two deposits of 1000, six months apart, worth 2200 after another six months.
        var flows = new[]
        {
            new CashFlow(new DateOnly(2025, 1, 1), -1000m),
            new CashFlow(new DateOnly(2025, 7, 1), -1000m),
            new CashFlow(new DateOnly(2026, 1, 1), 2200m)
        };

        var rate = Performance.Xirr(flows);

        rate.Should().NotBeNull();
        rate!.Value.Should().BeInRange(0.13m, 0.14m, "the second thousand only had half a year to grow");
    }

    [Fact]
    public void Xirr_Should_Be_Null_When_Nothing_Ever_Came_Back()
    {
        Performance.Xirr([new CashFlow(new DateOnly(2025, 1, 1), -1000m), new CashFlow(new DateOnly(2026, 1, 1), -1m)])
            .Should().BeNull();
    }

    [Fact]
    public void Compounding_At_Cdi_Should_Apply_Each_Business_Day_After_The_Deposit()
    {
        var from = new DateOnly(2026, 3, 2);   // a Monday
        var cdi = FlatCdi(from, new DateOnly(2026, 3, 13), 0.05m);
        var flows = new[] { new CashFlow(from, -1000m) };

        // Ten weekdays after the deposit day, each at 0,05%.
        var expected = 1000m * (decimal)Math.Pow(1.0005, 9);
        Performance.CompoundAtCdi(flows, cdi, new DateOnly(2026, 3, 13)).Should().BeApproximately(expected, 0.01m,
            "the deposit day's own rate is not earned, the nine that follow are");
    }

    [Fact]
    public void Annualized_Cdi_Should_Compound_On_A_252_Day_Year()
    {
        var from = new DateOnly(2025, 1, 1);
        var cdi = FlatCdi(from, new DateOnly(2025, 12, 31), 0.05m);

        var annual = Performance.AnnualizedCdi(cdi, from, new DateOnly(2025, 12, 31));

        annual.Should().BeApproximately((decimal)(Math.Pow(1.0005, 252) - 1), 0.0001m);
    }

    [Fact]
    public void Summary_Should_Compare_The_Portfolio_To_The_Same_Flows_At_Cdi()
    {
        var start = new DateOnly(2025, 1, 2);
        var end = new DateOnly(2026, 1, 2);
        var trades = new[] { new Trade("PETR4", AssetClass.Stock, TradeSide.Buy, 100, 10m, 0m, start, Someone) };
        var cdi = FlatCdi(start, end, 0.04m);

        var summary = Performance.Summarize(Performance.CashFlows(trades, []), marketValue: 1200m, cdi, end);

        summary.NetContributions.Should().Be(1000m);
        summary.MarketValue.Should().Be(1200m);
        summary.AnnualReturn.Should().BeApproximately(20m, 0.1m);
        summary.CdiValue.Should().BeGreaterThan(1000m).And.BeLessThan(1200m);
        summary.PercentOfCdi.Should().BeGreaterThan(100m);
        summary.Series.Should().HaveCountGreaterThan(12);
        summary.Series.Last().Date.Should().Be(end);
        summary.Series.Last().NetContributions.Should().Be(1000m);
    }

    [Fact]
    public void Cash_Flows_Should_Treat_Buys_As_Out_And_Sales_And_Payouts_As_In()
    {
        var trades = new[]
        {
            new Trade("PETR4", AssetClass.Stock, TradeSide.Buy, 100, 10m, 5m, new DateOnly(2026, 1, 10), Someone),
            new Trade("PETR4", AssetClass.Stock, TradeSide.Sell, 50, 12m, 3m, new DateOnly(2026, 3, 10), Someone)
        };
        var payouts = new[] { new Payout("PETR4", PayoutKind.Dividend, new DateOnly(2026, 2, 10), 100, 30m, 0m, Someone) };

        var flows = Performance.CashFlows(trades, payouts);

        flows.Select(f => f.Amount).Should().Equal(-1005m, 30m, 597m);
    }
}
