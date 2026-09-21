using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Investing;

namespace TrustFinance.Tests.Investing;

public class PayoutTests
{
    private const int Someone = 1;
    private static readonly Dictionary<string, Quote> NoQuotes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly DateOnly Today = new(2026, 9, 20);

    private static Trade Buy(string ticker, decimal qty, decimal price, string date)
        => new(ticker, AssetClass.Fii, TradeSide.Buy, qty, price, 0m, DateOnly.Parse(date), Someone);

    private static Payout Income(string ticker, decimal gross, string date, PayoutKind kind = PayoutKind.FundIncome, decimal tax = 0m)
        => new(ticker, kind, DateOnly.Parse(date), 100, gross, tax, Someone);

    [Fact]
    public void Payouts_Should_Count_Towards_The_Result_And_The_Yield_On_Cost()
    {
        var trades = new[] { Buy("HGLG11", 100, 100m, "2025-01-10") };
        var payouts = new[]
        {
            Income("HGLG11", 100m, "2026-03-10"),
            Income("HGLG11", 100m, "2026-06-10"),
            Income("HGLG11", 100m, "2025-06-10")   // more than twelve months ago
        };

        var p = Portfolio.Build(trades, [], payouts, NoQuotes, Today).Positions.Single();

        p.PayoutsReceived.Should().Be(300m);
        p.PayoutsTrailingYear.Should().Be(200m);
        p.YieldOnCost.Should().Be(2m, "R$ 200 over the last year on R$ 10.000 of cost");
        p.TotalPnL.Should().Be(300m, "no quote, so nothing on paper — only what was paid out");
    }

    [Fact]
    public void Yield_On_Cost_Should_Not_Inflate_When_The_Position_Shrinks_After_The_Payout()
    {
        var trades = new[]
        {
            Buy("HGLG11", 1000, 100m, "2025-01-10"),
            new Trade("HGLG11", AssetClass.Fii, TradeSide.Sell, 900, 100m, 0m, new DateOnly(2026, 8, 10), Someone)
        };
        var payouts = new[] { new Payout("HGLG11", PayoutKind.FundIncome, new DateOnly(2026, 6, 10), 1000, 1000m, 0m, Someone) };

        var p = Portfolio.Build(trades, [], payouts, NoQuotes, Today).Positions.Single();

        p.Quantity.Should().Be(100);
        p.YieldOnCost.Should().Be(1m, "R$ 1 per share over R$ 100 per share, whatever is still held");
    }

    [Fact]
    public void Interest_On_Equity_Should_Net_Out_The_Withholding()
    {
        var payout = Income("ITSA4", 1000m, "2026-05-10", PayoutKind.InterestOnEquity, tax: 150m);

        payout.NetAmount.Should().Be(850m);
        payout.GrossPerShare.Should().Be(10m);
        PayoutKind.InterestOnEquity.WithholdingRate().Should().Be(0.15m);
        PayoutKind.Dividend.WithholdingRate().Should().Be(0m);
    }

    [Fact]
    public void A_Closed_Position_That_Paid_Out_Should_Still_Be_Listed()
    {
        var trades = new[]
        {
            Buy("HGLG11", 100, 100m, "2025-01-10"),
            new Trade("HGLG11", AssetClass.Fii, TradeSide.Sell, 100, 100m, 0m, new DateOnly(2025, 6, 10), Someone)
        };

        var summary = Portfolio.Build(trades, [], [Income("HGLG11", 80m, "2025-03-10")], NoQuotes, Today);

        summary.Positions.Should().ContainSingle(p => !p.IsOpen && p.PayoutsReceived == 80m);
        summary.PayoutsReceived.Should().Be(80m);
    }

    [Theory]
    [InlineData(0, 100, 0, "Quantity")]
    [InlineData(100, 0, 0, "GrossAmount")]
    [InlineData(100, 100, 150, "WithheldTax")]
    public void A_Bad_Payout_Should_Say_Which_Field(decimal qty, decimal gross, decimal tax, string key)
    {
        var p = new Payout("PETR4", PayoutKind.Dividend, new DateOnly(2026, 1, 1), qty, gross, tax, Someone);

        p.Notifications.Should().Contain(n => n.Key == key);
    }
}
