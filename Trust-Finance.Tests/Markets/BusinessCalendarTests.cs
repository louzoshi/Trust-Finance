using TrustFinance.Domain.Markets;

namespace TrustFinance.Tests.Markets;

public class BusinessCalendarTests
{
    [Theory]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    public void Easter_Should_Match_The_Published_Dates(int year, int month, int day)
    {
        BusinessCalendar.Easter(year).Should().Be(new DateOnly(year, month, day));
    }

    [Theory]
    [InlineData(2026, 1, 1)]    // Confraternização
    [InlineData(2026, 2, 16)]   // Carnaval, segunda
    [InlineData(2026, 2, 17)]   // Carnaval, terça
    [InlineData(2026, 4, 3)]    // Sexta-feira Santa
    [InlineData(2026, 4, 21)]   // Tiradentes
    [InlineData(2026, 5, 1)]    // Trabalho
    [InlineData(2026, 6, 4)]    // Corpus Christi
    [InlineData(2026, 9, 7)]    // Independência
    [InlineData(2026, 10, 12)]  // Aparecida
    [InlineData(2026, 11, 2)]   // Finados
    [InlineData(2026, 11, 15)]  // República
    [InlineData(2026, 11, 20)]  // Consciência Negra
    [InlineData(2026, 12, 25)]  // Natal
    public void The_National_Holidays_Should_Not_Be_Business_Days(int y, int m, int d)
    {
        var date = new DateOnly(y, m, d);

        BusinessCalendar.IsHoliday(date).Should().BeTrue();
        BusinessCalendar.IsBusinessDay(date).Should().BeFalse();
    }

    [Fact]
    public void Black_Consciousness_Day_Should_Count_Only_From_2024()
    {
        BusinessCalendar.IsHoliday(new DateOnly(2023, 11, 20)).Should().BeFalse("it became national with Lei 14.759/2023");
        BusinessCalendar.IsHoliday(new DateOnly(2024, 11, 20)).Should().BeTrue();
    }

    [Fact]
    public void A_Year_Should_Have_Roughly_The_Two_Hundred_And_Fifty_Two_Days_The_Convention_Assumes()
    {
        // 2025 lands exactly on the convention; 2026 has three holidays on weekdays that
        // 2025 had on a weekend. The convention is a definition, not a count.
        BusinessCalendar.Count(new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1)).Should().Be(252);
        BusinessCalendar.Count(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)).Should().Be(249);
    }

    [Fact]
    public void Counting_Should_Include_The_Start_And_Exclude_The_End()
    {
        // Monday to Friday of the same week: four days accrue, not five.
        BusinessCalendar.Count(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 18)).Should().Be(4);
        BusinessCalendar.Count(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 14)).Should().Be(0, "bought and sold the same day earns nothing");
    }

    [Fact]
    public void A_Weekend_And_A_Holiday_Should_Be_Skipped()
    {
        BusinessCalendar.Count(new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 9))
            .Should().Be(2, "Friday and Tuesday; Saturday, Sunday and 7 September are not business days");
    }

    [Fact]
    public void A_Half_Year_Should_Count_What_The_Calendar_Says()
    {
        BusinessCalendar.Count(new DateOnly(2026, 1, 2), new DateOnly(2026, 7, 1)).Should().Be(122);
    }

    [Fact]
    public void Adding_Business_Days_Should_Jump_Weekends_And_Holidays()
    {
        // Friday 4 September + 1 is Tuesday 8th: the 7th is Independence Day.
        BusinessCalendar.Add(new DateOnly(2026, 9, 4), 1).Should().Be(new DateOnly(2026, 9, 8));
        BusinessCalendar.Add(new DateOnly(2026, 9, 8), -1).Should().Be(new DateOnly(2026, 9, 4));
        BusinessCalendar.Add(new DateOnly(2026, 9, 14), 5).Should().Be(new DateOnly(2026, 9, 21));
    }

    [Fact]
    public void Rolling_Should_Land_On_A_Business_Day_In_The_Asked_Direction()
    {
        var saturday = new DateOnly(2026, 9, 5);

        BusinessCalendar.NextOrSame(saturday).Should().Be(new DateOnly(2026, 9, 8), "the 7th is a holiday");
        BusinessCalendar.PreviousOrSame(saturday).Should().Be(new DateOnly(2026, 9, 4));
        BusinessCalendar.NextOrSame(new DateOnly(2026, 9, 4)).Should().Be(new DateOnly(2026, 9, 4));
    }
}
