using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Finance;

namespace TrustFinance.Tests.Domain;

public class ScheduleTests
{
    [Fact]
    public void Monthly_Should_Clamp_To_Month_End_Without_Drifting()
    {
        var anchor = new DateOnly(2026, 1, 31);

        Schedule.Occurrence(anchor, Frequency.Monthly, 1).Should().Be(new DateOnly(2026, 2, 28));
        Schedule.Occurrence(anchor, Frequency.Monthly, 2).Should().Be(new DateOnly(2026, 3, 31),
            "the third occurrence is computed from the anchor, not from February's 28th");
    }

    [Fact]
    public void Weekly_Should_Step_Seven_Days()
    {
        var anchor = new DateOnly(2026, 9, 7);

        Schedule.Occurrence(anchor, Frequency.Weekly, 3).Should().Be(new DateOnly(2026, 9, 28));
    }

    [Fact]
    public void Yearly_Should_Handle_A_Leap_Day()
    {
        var anchor = new DateOnly(2024, 2, 29);

        Schedule.Occurrence(anchor, Frequency.Yearly, 1).Should().Be(new DateOnly(2025, 2, 28));
        Schedule.Occurrence(anchor, Frequency.Yearly, 4).Should().Be(new DateOnly(2028, 2, 29));
    }

    [Theory]
    [InlineData(2026, 9, 5, 2026, 9, 5)]   // on an occurrence: that one
    [InlineData(2026, 9, 6, 2026, 10, 5)]  // the day after: next month
    [InlineData(2026, 1, 1, 2026, 3, 5)]   // before the anchor: the anchor itself
    [InlineData(2027, 3, 4, 2027, 3, 5)]   // a year on, the day before
    public void NextOnOrAfter_Should_Find_The_First_Monthly_Occurrence_Not_Before_The_Date(
        int y, int m, int d, int ey, int em, int ed)
    {
        var anchor = new DateOnly(2026, 3, 5);

        Schedule.NextOnOrAfter(anchor, Frequency.Monthly, new DateOnly(y, m, d))
            .Should().Be(new DateOnly(ey, em, ed));
    }

    [Fact]
    public void NextOnOrAfter_Should_Work_For_Weekly_And_Yearly()
    {
        var anchor = new DateOnly(2026, 9, 7);

        Schedule.NextOnOrAfter(anchor, Frequency.Weekly, new DateOnly(2026, 9, 20)).Should().Be(new DateOnly(2026, 9, 21));
        Schedule.NextOnOrAfter(anchor, Frequency.Yearly, new DateOnly(2026, 9, 8)).Should().Be(new DateOnly(2027, 9, 7));
    }

    [Fact]
    public void NextAfter_Should_Skip_An_Exact_Occurrence()
    {
        var anchor = new DateOnly(2026, 3, 5);

        Schedule.NextAfter(anchor, Frequency.Monthly, new DateOnly(2026, 9, 5)).Should().Be(new DateOnly(2026, 10, 5));
    }
}
