using TrustFinance.Domain.Markets;

namespace TrustFinance.Tests.Markets;

public class YieldCurveTests
{
    private static readonly YieldCurve Curve = new(
    [
        new CurvePoint(21, 10m),
        new CurvePoint(126, 11m),
        new CurvePoint(252, 12m),
        new CurvePoint(756, 13m)
    ]);

    [Fact]
    public void A_Quoted_Vertex_Should_Return_Its_Own_Rate()
    {
        Curve.RateFor(126).Should().BeApproximately(11m, 0.000001m);
        Curve.RateFor(252).Should().BeApproximately(12m, 0.000001m);
    }

    [Fact]
    public void Between_Vertices_The_Rate_Should_Sit_Between_Them()
    {
        var mid = Curve.RateFor(189);

        mid.Should().BeGreaterThan(11m).And.BeLessThan(12m);
    }

    [Fact]
    public void Interpolation_Should_Be_Flat_Forward_Not_Linear_On_The_Rate()
    {
        // Flat forward interpolates the accumulated factor, so the rate curve bends;
        // the straight-line answer would be exactly halfway.
        var mid = Curve.RateFor(189);
        var linear = 11.5m;

        mid.Should().NotBeApproximately(linear, 0.0001m);
        mid.Should().BeApproximately(11.6657m, 0.001m, "the factor, not the rate, moves in a straight line");
    }

    [Fact]
    public void The_Forward_Between_Two_Vertices_Should_Be_Constant()
    {
        // The defining property: the forward rate from any interior point to the next
        // vertex is the same as from the previous vertex to that point.
        decimal Forward(int from, int to)
        {
            var f = Rates.Factor(Curve.RateFor(to), to) / Rates.Factor(Curve.RateFor(from), from);
            return Rates.AnnualFrom(f, to - from);
        }

        var first = Forward(126, 160);
        var second = Forward(160, 200);
        var third = Forward(200, 252);

        second.Should().BeApproximately(first, 0.01m);
        third.Should().BeApproximately(first, 0.01m);
    }

    [Fact]
    public void Outside_The_Quoted_Range_The_Curve_Should_Be_Flat()
    {
        Curve.RateFor(5).Should().Be(10m, "shorter than the first vertex");
        Curve.RateFor(2000).Should().Be(13m, "longer than the last; the market never quoted it");
    }

    [Fact]
    public void A_Discount_Factor_Should_Undo_Its_Own_Compounding()
    {
        var days = 189;
        var factor = Rates.Factor(Curve.RateFor(days), days);

        (Curve.DiscountFactor(days) * factor).Should().BeApproximately(1m, 0.0000001m);
    }

    [Fact]
    public void A_Flat_Curve_Should_Answer_The_Same_Rate_Everywhere()
    {
        var flat = YieldCurve.Flat(13.5m);

        flat.RateFor(21).Should().Be(13.5m);
        flat.RateFor(1260).Should().Be(13.5m);
    }

    [Fact]
    public void A_Curve_Needs_At_Least_One_Vertex()
    {
        var act = () => new YieldCurve([]);

        act.Should().Throw<ArgumentException>();
    }
}
