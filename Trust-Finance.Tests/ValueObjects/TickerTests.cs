using TrustFinance.Domain.ValueObjects;

namespace TrustFinance.Tests.ValueObjects;

public class TickerTests
{
    [Theory]
    [InlineData("petr4", "PETR4")]
    [InlineData("  PETR4  ", "PETR4")]
    [InlineData("Petr4", "PETR4")]
    [InlineData("tesouro-selic-2029", "TESOURO-SELIC-2029")]
    public void Should_Normalize_To_Upper_Case(string input, string expected)
    {
        var ticker = new Ticker(input);

        ticker.IsValid.Should().BeTrue();
        ticker.Value.Should().Be(expected);
    }

    [Fact]
    public void Two_Spellings_Of_The_Same_Symbol_Should_Be_Equal()
    {
        new Ticker(" petr4 ").Should().Be(new Ticker("PETR4"));
        new Ticker("petr4").GetHashCode().Should().Be(new Ticker("PETR4").GetHashCode());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Reject_An_Empty_Symbol(string? input)
    {
        var ticker = new Ticker(input);

        ticker.IsInvalid.Should().BeTrue();
        ticker.Notifications.Should().ContainSingle();
    }

    [Theory]
    [InlineData("P")]
    [InlineData("TICKER-QUE-NAO-ACABA-NUNCA")]
    public void Should_Reject_A_Bad_Length(string input) =>
        new Ticker(input).IsInvalid.Should().BeTrue();

    [Theory]
    [InlineData("PETR 4")]
    [InlineData("PETR4'")]
    [InlineData("PETR4/../etc")]
    public void Should_Reject_Characters_That_Do_Not_Belong_In_A_Symbol(string input)
    {
        // These travel straight into a market-data URL, so the guard is not cosmetic.
        new Ticker(input).IsInvalid.Should().BeTrue();
    }
}
