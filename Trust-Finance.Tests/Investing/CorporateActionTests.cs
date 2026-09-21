using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Investing;

namespace TrustFinance.Tests.Investing;

public class CorporateActionTests
{
    private const int Someone = 1;
    private static readonly Dictionary<string, Quote> NoQuotes = new(StringComparer.OrdinalIgnoreCase);

    private static Trade Buy(string ticker, decimal qty, decimal price, string date, AssetClass c = AssetClass.Stock)
        => new(ticker, c, TradeSide.Buy, qty, price, 0m, DateOnly.Parse(date), Someone);

    private static Trade Sell(string ticker, decimal qty, decimal price, string date)
        => new(ticker, AssetClass.Stock, TradeSide.Sell, qty, price, 0m, DateOnly.Parse(date), Someone);

    private static CorporateAction Split(string ticker, decimal factor, string date)
        => new(ticker, CorporateActionKind.Split, DateOnly.Parse(date), factor, null, Someone);

    private static CorporateAction Reverse(string ticker, decimal factor, string date)
        => new(ticker, CorporateActionKind.ReverseSplit, DateOnly.Parse(date), factor, null, Someone);

    private static CorporateAction Bonus(string ticker, decimal factor, decimal unitCost, string date)
        => new(ticker, CorporateActionKind.Bonus, DateOnly.Parse(date), factor, unitCost, Someone);

    private static Position Only(IEnumerable<Trade> trades, IEnumerable<CorporateAction> actions)
        => Portfolio.Build(trades, actions, [], NoQuotes, null).Positions.Single();

    [Fact]
    public void A_Split_Should_Multiply_The_Quantity_And_Divide_The_Average_Price()
    {
        var p = Only([Buy("MGLU3", 100, 30m, "2026-01-10")], [Split("MGLU3", 10, "2026-02-01")]);

        p.Quantity.Should().Be(1000);
        p.AveragePrice.Should().Be(3m);
        p.Invested.Should().Be(3000m, "the cost basis is untouched");
    }

    [Fact]
    public void A_Reverse_Split_Should_Drop_The_Fraction()
    {
        var p = Only([Buy("OIBR3", 105, 3m, "2026-01-10")], [Reverse("OIBR3", 0.1m, "2026-02-01")]);

        p.Quantity.Should().Be(10, "10,5 shares cannot exist; the half is sold off as leftovers");
        p.AveragePrice.Should().Be(30m);
    }

    [Fact]
    public void A_Bonus_Should_Add_Shares_At_The_Declared_Cost()
    {
        var p = Only([Buy("ITSA4", 100, 30m, "2026-01-10")], [Bonus("ITSA4", 0.10m, 5m, "2026-02-01")]);

        p.Quantity.Should().Be(110);
        p.AveragePrice.Should().BeApproximately(27.7273m, 0.0001m, "(3000 + 10 x 5) / 110");
        p.Invested.Should().Be(3050m);
    }

    [Fact]
    public void An_Event_On_The_Same_Day_As_A_Trade_Should_Apply_First()
    {
        // The sale was made at post-split prices, so the position it comes out of must
        // already be split too — otherwise 100 shares at R$ 15 look like a 50% loss.
        var p = Only(
            [Buy("MGLU3", 100, 30m, "2026-01-10"), Sell("MGLU3", 100, 15m, "2026-02-01")],
            [Split("MGLU3", 2, "2026-02-01")]);

        p.Quantity.Should().Be(100);
        p.AveragePrice.Should().Be(15m);
        p.RealizedPnL.Should().Be(0m);
    }

    [Fact]
    public void An_Event_Before_The_First_Trade_Should_Be_Ignored()
    {
        var p = Only([Buy("MGLU3", 100, 30m, "2026-03-10")], [Split("MGLU3", 10, "2026-02-01")]);

        p.Quantity.Should().Be(100);
    }

    [Theory]
    [InlineData(CorporateActionKind.Split, 1.0, "Factor")]
    [InlineData(CorporateActionKind.Split, 0.5, "Factor")]
    [InlineData(CorporateActionKind.ReverseSplit, 2.0, "Factor")]
    [InlineData(CorporateActionKind.Bonus, 0, "Factor")]
    public void A_Factor_That_Contradicts_The_Kind_Should_Be_Rejected(CorporateActionKind kind, double factor, string key)
    {
        var a = new CorporateAction("PETR4", kind, new DateOnly(2026, 1, 1), (decimal)factor, 0m, Someone);

        a.Notifications.Should().Contain(n => n.Key == key);
    }

    [Fact]
    public void A_Bonus_Without_A_Unit_Cost_Should_Be_Rejected()
    {
        var a = new CorporateAction("PETR4", CorporateActionKind.Bonus, new DateOnly(2026, 1, 1), 0.1m, null, Someone);

        a.Notifications.Should().Contain(n => n.Key == "BonusUnitCost");
    }
}
