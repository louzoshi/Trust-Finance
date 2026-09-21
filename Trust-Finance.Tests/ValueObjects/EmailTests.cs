using TrustFinance.Domain.ValueObjects;

namespace TrustFinance.Tests.ValueObjects;

public class EmailTests
{
    [Theory]
    [InlineData("Ana@Example.com ", "ana@example.com")]
    [InlineData("  ANA@EXAMPLE.COM", "ana@example.com")]
    public void Should_Normalize_So_One_Person_Is_One_Account(string input, string expected)
    {
        var email = new Email(input);

        email.IsValid.Should().BeTrue();
        email.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("ana@example.com")]
    [InlineData("ana.silva+tag@sub.example.com.br")]
    public void Should_Accept_A_Plausible_Address(string input) =>
        new Email(input).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sem-arroba")]
    [InlineData("dois@@example.com")]
    [InlineData("sem@dominio")]
    [InlineData("com espaco@example.com")]
    public void Should_Reject_What_Is_Clearly_Not_An_Address(string? input) =>
        new Email(input).IsInvalid.Should().BeTrue();

    [Fact]
    public void Should_Reject_An_Address_Longer_Than_The_Column()
    {
        var tooLong = new string('a', Email.MaxLength) + "@example.com";

        new Email(tooLong).IsInvalid.Should().BeTrue();
    }
}
