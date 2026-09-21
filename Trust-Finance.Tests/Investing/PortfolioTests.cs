using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Investing;

namespace TrustFinance.Tests.Investing;

public class PortfolioTests
{
    private const int Someone = 1;

    private static readonly Dictionary<string, Quote> NoQuotes = new(StringComparer.OrdinalIgnoreCase);

    private static Trade Buy(string ticker, decimal qty, decimal price, decimal fees = 0, string date = "2026-01-10")
        => new(ticker, AssetClass.Stock, TradeSide.Buy, qty, price, fees, DateOnly.Parse(date), Someone);

    private static Trade Sell(string ticker, decimal qty, decimal price, decimal fees = 0, string date = "2026-02-10")
        => new(ticker, AssetClass.Stock, TradeSide.Sell, qty, price, fees, DateOnly.Parse(date), Someone);

    private static Dictionary<string, Quote> Quoted(string ticker, decimal price, decimal? previousClose = null)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            [ticker] = new Quote(ticker, ticker, AssetClass.Stock, price, previousClose)
        };

    [Fact]
    public void Average_Price_Should_Weight_By_Quantity()
    {
        var trades = new[] { Buy("PETR4", 100, 30m), Buy("PETR4", 100, 40m) };

        var position = Portfolio.Build(trades, NoQuotes).Positions.Single();

        position.Quantity.Should().Be(200);
        position.AveragePrice.Should().Be(35m);
        position.Invested.Should().Be(7000m);
    }

    [Fact]
    public void Buy_Fees_Should_Raise_The_Cost_Basis()
    {
        // 100 x R$ 30 plus R$ 10 of costs is R$ 30,10 a share, not R$ 30.
        var trades = new[] { Buy("PETR4", 100, 30m, fees: 10m) };

        var position = Portfolio.Build(trades, NoQuotes).Positions.Single();

        position.AveragePrice.Should().Be(30.10m);
    }

    [Fact]
    public void Selling_Should_Bank_The_Gain_And_Leave_The_Average_Price_Alone()
    {
        var trades = new Trade[] { Buy("PETR4", 100, 30m), Sell("PETR4", 40, 50m) };

        var position = Portfolio.Build(trades, NoQuotes).Positions.Single();

        position.Quantity.Should().Be(60);
        position.AveragePrice.Should().Be(30m, "a partial sale does not change what the rest cost");
        position.RealizedPnL.Should().Be(800m, "40 shares sold at a R$ 20 gain each");
    }

    [Fact]
    public void Sell_Fees_Should_Reduce_The_Realized_Gain()
    {
        var trades = new Trade[] { Buy("PETR4", 100, 30m), Sell("PETR4", 100, 40m, fees: 25m) };

        Portfolio.Build(trades, NoQuotes).Positions.Single().RealizedPnL.Should().Be(975m);
    }

    [Fact]
    public void Unrealized_Should_Come_From_The_Quote()
    {
        var trades = new[] { Buy("PETR4", 100, 30m) };

        var position = Portfolio.Build(trades, Quoted("PETR4", 38m)).Positions.Single();

        position.MarketValue.Should().Be(3800m);
        position.UnrealizedPnL.Should().Be(800m);
        position.UnrealizedPercent.Should().BeApproximately(26.67m, 0.01m);
    }

    [Fact]
    public void A_Position_Without_A_Quote_Should_Be_Held_At_Cost_And_Flagged()
    {
        var trades = new[] { Buy("XPTO3", 10, 5m) };

        var summary = Portfolio.Build(trades, NoQuotes);

        summary.MarketValue.Should().Be(50m, "cost is the honest fallback, not zero");
        summary.UnrealizedPnL.Should().Be(0m);
        summary.TickersWithoutQuote.Should().Be(1);
        summary.IsPartiallyPriced.Should().BeTrue();
    }

    [Fact]
    public void A_Fully_Sold_Ticker_Should_Keep_Its_Realized_Result_But_Leave_The_Allocation()
    {
        var trades = new Trade[] { Buy("PETR4", 100, 30m), Sell("PETR4", 100, 40m) };

        var summary = Portfolio.Build(trades, Quoted("PETR4", 41m));

        var position = summary.Positions.Single();
        position.IsOpen.Should().BeFalse();
        position.RealizedPnL.Should().Be(1000m);
        summary.MarketValue.Should().Be(0m, "nothing is held any more");
        summary.Allocation.Should().BeEmpty();
    }

    [Fact]
    public void Selling_More_Than_Is_Held_Should_Not_Produce_A_Negative_Position()
    {
        var trades = new Trade[] { Buy("PETR4", 10, 30m), Sell("PETR4", 999, 40m) };

        var position = Portfolio.Build(trades, NoQuotes).Positions.Single();

        position.Quantity.Should().Be(0);
        position.RealizedPnL.Should().Be(100m, "only the 10 actually held could be sold");
    }

    [Fact]
    public void Average_Price_Should_Depend_On_Trade_Order_Not_Input_Order()
    {
        var chronological = new Trade[]
        {
            Buy("PETR4", 100, 30m, date: "2026-01-10"),
            Sell("PETR4", 50, 35m, date: "2026-01-20"),
            Buy("PETR4", 50, 40m, date: "2026-01-30")
        };

        var shuffled = new[] { chronological[2], chronological[0], chronological[1] };

        var a = Portfolio.Build(chronological, NoQuotes).Positions.Single();
        var b = Portfolio.Build(shuffled, NoQuotes).Positions.Single();

        b.Should().Be(a);
        a.AveragePrice.Should().Be(35m);
        a.RealizedPnL.Should().Be(250m);
    }

    [Fact]
    public void Allocation_Should_Split_By_Class_And_Sum_To_A_Hundred()
    {
        var stock = Buy("PETR4", 100, 30m);
        var fii = new Trade("HGLG11", AssetClass.Fii, TradeSide.Buy, 10, 150m, 0, new DateOnly(2026, 1, 10), Someone);

        var quotes = new Dictionary<string, Quote>(StringComparer.OrdinalIgnoreCase)
        {
            ["PETR4"] = new("PETR4", "Petrobras", AssetClass.Stock, 30m),
            ["HGLG11"] = new("HGLG11", "CSHG Logística", AssetClass.Fii, 150m)
        };

        var allocation = Portfolio.Build([stock, fii], quotes).Allocation;

        allocation.Should().HaveCount(2);
        allocation.Sum(a => a.Percent).Should().BeApproximately(100m, 0.01m);
        allocation.Single(a => a.Class == AssetClass.Stock).Percent.Should().BeApproximately(66.67m, 0.01m);
    }

    [Fact]
    public void Day_Change_Should_Aggregate_Across_Positions()
    {
        var trades = new[] { Buy("PETR4", 100, 30m) };

        var summary = Portfolio.Build(trades, Quoted("PETR4", 32m, previousClose: 30m));

        summary.DayChange.Should().Be(200m);
        summary.DayChangePercent.Should().BeApproximately(6.67m, 0.01m);
    }

    [Fact]
    public void Tickers_Should_Be_Distinct_And_Case_Insensitive()
    {
        var trades = new[] { Buy("petr4", 1, 1m), Buy("PETR4", 1, 1m), Buy("VALE3", 1, 1m) };

        Portfolio.Tickers(trades).Should().HaveCount(2);
    }
}
