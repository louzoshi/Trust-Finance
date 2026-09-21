using TrustFinance.Domain.Markets;

namespace TrustFinance.Tests.Markets;

public class RatesTests
{
    [Fact]
    public void A_Year_Of_Business_Days_Should_Return_Exactly_The_Annual_Rate()
    {
        Rates.Factor(12m, 252).Should().BeApproximately(1.12m, 0.0000001m);
    }

    [Fact]
    public void The_Daily_Factor_Compounded_Should_Rebuild_The_Year()
    {
        var daily = Rates.DailyFactor(12m);

        ((decimal)Math.Pow((double)daily, 252)).Should().BeApproximately(1.12m, 0.000001m);
    }

    [Fact]
    public void Half_A_Year_Should_Be_The_Square_Root_Not_Half_The_Rate()
    {
        var half = Rates.Factor(12m, 126);

        half.Should().BeApproximately((decimal)Math.Sqrt(1.12), 0.000001m);
        half.Should().BeLessThan(1.06m, "compounding is not proportional");
    }

    [Fact]
    public void An_Annual_Rate_Should_Come_Back_From_Its_Factor()
    {
        Rates.AnnualFrom(1.12m, 252).Should().BeApproximately(12m, 0.0001m);
        Rates.AnnualFrom(Rates.Factor(9.85m, 63), 63).Should().BeApproximately(9.85m, 0.0001m);
    }

    [Fact]
    public void No_Days_Should_Earn_Nothing()
    {
        Rates.Factor(12m, 0).Should().Be(1m);
        Rates.AnnualFrom(1.5m, 0).Should().Be(0m);
    }

    [Fact]
    public void A_Percentage_Of_The_Cdi_Should_Cut_Each_Day_Before_Compounding()
    {
        // Ten days at a published 0,05% a day, at 110% of the CDI.
        var days = Enumerable.Range(0, 10)
            .Select(i => new DailyRate(new DateOnly(2026, 3, 2).AddDays(i), 0.05m))
            .ToList();

        var expected = (decimal)Math.Pow(1 + 0.0005 * 1.10, 10);

        Rates.PercentOfCdiFactor(days, 110m).Should().BeApproximately(expected, 0.0000001m);
    }

    [Fact]
    public void Scaling_The_Accumulated_Factor_Instead_Of_Each_Day_Should_Give_A_Different_Answer()
    {
        var days = Enumerable.Range(0, 252)
            .Select(i => new DailyRate(new DateOnly(2026, 1, 1).AddDays(i), 0.05m))
            .ToList();

        decimal Naive(decimal percent) => 1 + (Rates.IndexFactor(days) - 1) * (percent / 100m);

        // Compounding a day at a time is convex, so the naive shortcut lands on the
        // wrong side in both directions: it understates paper above 100% of the CDI and
        // overstates paper below it. Either way it is not the contract CETIP settles.
        Rates.PercentOfCdiFactor(days, 110m).Should().BeGreaterThan(Naive(110m));
        Rates.PercentOfCdiFactor(days, 90m).Should().BeLessThan(Naive(90m));

        // At exactly 100% the two agree, which is why the error hides in testing.
        Rates.PercentOfCdiFactor(days, 100m).Should().BeApproximately(Naive(100m), 0.0000001m);
    }

    [Fact]
    public void An_Index_Should_Accumulate_Day_By_Day()
    {
        var days = new[]
        {
            new DailyRate(new DateOnly(2026, 3, 2), 0.05m),
            new DailyRate(new DateOnly(2026, 3, 3), 0.04m)
        };

        Rates.IndexFactor(days).Should().BeApproximately(1.0005m * 1.0004m, 0.0000001m);
    }
}
