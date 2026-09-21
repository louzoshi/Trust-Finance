using TrustFinance.App;

namespace TrustFinance.Tests.Domain;

public class PeriodTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);

    [Fact]
    public void ThisMonth_Should_Run_From_The_First_To_The_Last_Day()
    {
        Period.ThisMonth(Today).Should().Be(new Period(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void LastMonth_Should_Cross_A_Year_Boundary()
    {
        Period.LastMonth(new DateOnly(2027, 1, 15)).Should().Be(new Period(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void LastThreeMonths_Should_Include_This_One()
    {
        Period.LastThreeMonths(Today).Should().Be(new Period(new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void Contains_Should_Be_Inclusive_At_Both_Ends()
    {
        var p = Period.ThisMonth(Today);

        p.Contains(new DateOnly(2026, 9, 1)).Should().BeTrue();
        p.Contains(new DateOnly(2026, 9, 30)).Should().BeTrue();
        p.Contains(new DateOnly(2026, 10, 1)).Should().BeFalse();
        Period.All.Contains(new DateOnly(1999, 1, 1)).Should().BeTrue();
    }
}
