using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Markets;

namespace TrustFinance.Tests.Markets;

public class FgcTests
{
    private const int Someone = 1;
    private static readonly DateOnly Today = new(2026, 9, 21);

    private static FixedIncomeValuation Held(string issuer, decimal principal, FixedIncomeKind kind = FixedIncomeKind.Cdb)
        => FixedIncomePricing.Value(
            new FixedIncomeInvestment(issuer, kind, IndexKind.Fixed, 0m, principal,
                new DateOnly(2026, 9, 20), new DateOnly(2030, 1, 1), Someone),
            Today);

    [Fact]
    public void Everything_Under_The_Limit_Should_Be_Covered()
    {
        var exposure = Fgc.Exposure([Held("Banco A", 100_000m), Held("Banco B", 80_000m)]);

        exposure.Should().OnlyContain(e => !e.IsOverLimit);
        Fgc.CoveredTotal(exposure).Should().Be(180_000m);
    }

    [Fact]
    public void Two_Papers_At_The_Same_Bank_Should_Be_Added_Together()
    {
        var exposure = Fgc.Exposure([Held("Banco A", 200_000m), Held("banco a", 100_000m)]);

        exposure.Should().ContainSingle("the same issuer, whatever the casing");
        exposure[0].Covered.Should().Be(250_000m);
        exposure[0].Uncovered.Should().Be(50_000m);
        exposure[0].IsOverLimit.Should().BeTrue();
    }

    [Fact]
    public void Splitting_The_Same_Money_Across_Banks_Should_Cover_All_Of_It()
    {
        var exposure = Fgc.Exposure([Held("Banco A", 250_000m), Held("Banco B", 250_000m)]);

        exposure.Should().OnlyContain(e => !e.IsOverLimit);
        Fgc.CoveredTotal(exposure).Should().Be(500_000m);
    }

    [Fact]
    public void The_Four_Year_Ceiling_Should_Cap_The_Total()
    {
        var exposure = Fgc.Exposure(Enumerable.Range(1, 6).Select(i => Held($"Banco {i}", 250_000m)));

        exposure.Sum(e => e.Covered).Should().Be(1_500_000m);
        Fgc.CoveredTotal(exposure).Should().Be(1_000_000m, "the guarantee stops at a million every four years");
    }

    [Fact]
    public void Treasury_Paper_Should_Be_Left_Out_Rather_Than_Counted_As_Uncovered()
    {
        var exposure = Fgc.Exposure([Held("Tesouro Nacional", 900_000m, FixedIncomeKind.TesouroSelic)]);

        exposure.Should().BeEmpty("it is the sovereign; showing it as unguaranteed would read as a risk that is not there");
    }

    [Fact]
    public void Headroom_Should_Say_How_Much_More_Fits()
    {
        var exposure = Fgc.Exposure([Held("Banco A", 180_000m)]);

        exposure[0].Headroom.Should().Be(70_000m);
    }

    [Fact]
    public void A_Redeemed_Paper_Should_Not_Count()
    {
        var paper = new FixedIncomeInvestment("Banco A", FixedIncomeKind.Cdb, IndexKind.Fixed, 0m, 300_000m,
            new DateOnly(2026, 1, 10), new DateOnly(2030, 1, 1), Someone);
        paper.Redeem(new DateOnly(2026, 6, 1));

        Fgc.Exposure([FixedIncomePricing.Value(paper, Today)]).Should().BeEmpty();
    }
}
