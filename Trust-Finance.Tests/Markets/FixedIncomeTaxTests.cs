using TrustFinance.Domain.Markets;

namespace TrustFinance.Tests.Markets;

public class FixedIncomeTaxTests
{
    [Theory]
    [InlineData(1, 0.225)]
    [InlineData(180, 0.225)]
    [InlineData(181, 0.20)]
    [InlineData(360, 0.20)]
    [InlineData(361, 0.175)]
    [InlineData(720, 0.175)]
    [InlineData(721, 0.15)]
    [InlineData(3650, 0.15)]
    public void The_Regressive_Table_Should_Step_On_Its_Published_Boundaries(int days, double rate)
    {
        FixedIncomeTax.IncomeTaxRate(days).Should().Be((decimal)rate);
    }

    [Theory]
    [InlineData(1, 0.96)]
    [InlineData(15, 0.50)]
    [InlineData(29, 0.03)]
    [InlineData(30, 0)]
    [InlineData(60, 0)]
    public void Iof_Should_Fall_To_Nothing_On_The_Thirtieth_Day(int days, double rate)
    {
        FixedIncomeTax.IofRate(days).Should().Be((decimal)rate);
    }

    [Fact]
    public void A_Gain_Held_Past_Two_Years_Should_Pay_Fifteen_Percent_And_No_Iof()
    {
        var tax = FixedIncomeTax.On(1000m, calendarDays: 800, exempt: false);

        tax.Iof.Should().Be(0m);
        tax.IncomeTaxRate.Should().Be(0.15m);
        tax.IncomeTax.Should().Be(150m);
        tax.Net.Should().Be(850m);
    }

    [Fact]
    public void Iof_Should_Be_Charged_First_And_Shrink_The_Income_Tax_Base()
    {
        // Ten days: IOF takes 66% of the gain, income tax 22,5% of what is left.
        var tax = FixedIncomeTax.On(100m, calendarDays: 10, exempt: false);

        tax.Iof.Should().Be(66m);
        tax.IncomeTax.Should().Be(7.65m, "22,5% of the remaining R$ 34, not of the original R$ 100");
        tax.Net.Should().Be(26.35m);
    }

    [Fact]
    public void An_Exempt_Paper_Should_Keep_Everything()
    {
        var tax = FixedIncomeTax.On(1000m, calendarDays: 30, exempt: true);

        tax.Total.Should().Be(0m);
        tax.Net.Should().Be(1000m);
    }

    [Fact]
    public void A_Loss_Should_Not_Be_Taxed()
    {
        FixedIncomeTax.On(-50m, calendarDays: 400, exempt: false).Total.Should().Be(0m);
    }
}
