using TrustFinance.Domain.ValueObjects;

namespace TrustFinance.Tests.Domain;

public class SlugTests
{
    [Theory]
    [InlineData("Alimentação", "alimentacao")]
    [InlineData("Mercado & Padaria", "mercado-padaria")]
    [InlineData("  Educação Física  ", "educacao-fisica")]
    [InlineData("Cartão de crédito", "cartao-de-credito")]
    [InlineData("---Lazer---", "lazer")]
    [InlineData("Ação!!!", "acao")]
    [InlineData("2ª via", "2-via")]
    public void From_Should_Strip_Accents_And_Join_With_Dashes(string name, string expected)
    {
        Slug.From(name).Should().Be(expected);
    }

    [Fact]
    public void From_Should_Be_Empty_When_Nothing_Survives()
    {
        Slug.From("!!!").Should().BeEmpty();
    }
}
