using TrustFinance.Domain.Banking;

namespace TrustFinance.Tests.Banking;

public class BoletoTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);

    // Built by hand: Bradesco (237), factor 1500, R$ 1.234,56, free field 1234567890123456789012345.
    private const string ValidBankLine = "23791.23454 67890.123457 67890.123457 9 15000000123456";

    [Fact]
    public void A_Valid_Bank_Line_Should_Yield_Bank_Value_And_Due_Date()
    {
        Boleto.TryParse(ValidBankLine, Today, out var info, out var error).Should().BeTrue(error);

        info.Kind.Should().Be(BoletoKind.Bank);
        info.BankCode.Should().Be("237");
        info.Amount.Should().Be(1234.56m);
        info.DueDate.Should().Be(new DateOnly(2026, 7, 7), "factor 1500 on the cycle that restarted at 1000 on 22/02/2025");
    }

    [Fact]
    public void Dots_And_Spaces_Should_Be_Ignored()
    {
        var bare = new string(ValidBankLine.Where(char.IsDigit).ToArray());

        Boleto.TryParse(bare, Today, out var a, out _).Should().BeTrue();
        Boleto.TryParse(ValidBankLine, Today, out var b, out _).Should().BeTrue();
        a.Should().Be(b);
    }

    [Fact]
    public void A_Mistyped_Field_Should_Be_Refused()
    {
        var typo = ValidBankLine.Replace("23791.23454", "23791.23455");

        Boleto.TryParse(typo, Today, out _, out var error).Should().BeFalse();
        error.Should().Contain("não confere");
    }

    [Fact]
    public void A_Line_Whose_Fields_Check_But_General_Digit_Does_Not_Should_Be_Refused()
    {
        // The example that circulates in tutorials: every field's digit checks, the
        // barcode's general digit does not. Accepting it would be accepting a number
        // no bank would.
        Boleto.TryParse("34191.79001 01043.510047 91020.150008 8 84550000026035", Today, out _, out var error).Should().BeFalse();
        error.Should().Contain("geral");
    }

    [Fact]
    public void A_Collection_Slip_Should_Yield_Its_Segment_And_Value()
    {
        // Segment 2 (water/sanitation), id 6 (modulo 10, value is money), R$ 123,45.
        Boleto.TryParse("826700000019 234500000000 000000000000 000000000000", Today, out var info, out var error).Should().BeTrue(error);

        info.Kind.Should().Be(BoletoKind.Collection);
        info.Segment.Should().Be("Saneamento");
        info.Amount.Should().Be(123.45m);
        info.DueDate.Should().BeNull("collection slips do not encode a due date");
    }

    [Fact]
    public void The_Wrong_Length_Should_Say_So()
    {
        Boleto.TryParse("1234", Today, out _, out var error).Should().BeFalse();
        error.Should().Contain("47");
    }

    [Theory]
    [InlineData(1000, 2025, 2, 22)]   // first day of the new cycle
    [InlineData(9999, 2025, 2, 21)]   // last day of the old one, still nearer than 2049
    public void The_Factor_Should_Pick_The_Cycle_Nearest_To_Today(int factor, int y, int m, int d)
    {
        Boleto.DueDateFromFactor(factor, new DateOnly(2025, 3, 1)).Should().Be(new DateOnly(y, m, d));
    }

    [Fact]
    public void Before_The_Rollover_Only_The_Old_Cycle_Exists()
    {
        Boleto.DueDateFromFactor(1000, new DateOnly(2000, 1, 1)).Should().Be(new DateOnly(2000, 7, 3));
    }

    [Theory]
    [InlineData("237912345", 4)]
    [InlineData("6789012345", 7)]
    public void Modulo_10_Should_Match_The_Febraban_Examples(string digits, int expected)
    {
        Boleto.Mod10(digits).Should().Be(expected);
    }
}
