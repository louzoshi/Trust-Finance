using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Tax;

namespace TrustFinance.Tests.Tax;

/// <summary>
/// Worked examples with the numbers fixed by hand. Each one is a rule from the
/// Receita's "Perguntas e Respostas" on variable income, turned into a case.
/// </summary>
public class CapitalGainsTests
{
    private const int Someone = 1;

    private static Trade Buy(string ticker, decimal qty, decimal price, string date, AssetClass c = AssetClass.Stock, decimal fees = 0m)
        => new(ticker, c, TradeSide.Buy, qty, price, fees, DateOnly.Parse(date), Someone);

    private static Trade Sell(string ticker, decimal qty, decimal price, string date, AssetClass c = AssetClass.Stock, decimal fees = 0m)
        => new(ticker, c, TradeSide.Sell, qty, price, fees, DateOnly.Parse(date), Someone);

    private static IReadOnlyList<MonthlyTax> Compute(params Trade[] trades) => CapitalGains.Compute(trades, []);

    [Fact]
    public void Stock_Sales_Under_Twenty_Thousand_Should_Make_The_Gain_Exempt()
    {
        var report = Compute(Buy("PETR4", 100, 30m, "2026-01-10"), Sell("PETR4", 100, 40m, "2026-02-10"));

        var feb = report.Single(m => m.Month == 2);
        feb.StockSales.Should().Be(4000m);
        feb.StockGainsExempt.Should().BeTrue();
        feb[TaxBucket.Common]!.ExemptGain.Should().Be(1000m);
        feb[TaxBucket.Common]!.TaxableBase.Should().Be(0m);
        feb.TaxDue.Should().Be(0m);
        feb.Withheld.Should().Be(0m, "0,005% of R$ 4.000 is 20 cents, under the R$ 1 the broker bothers with");
    }

    [Fact]
    public void Stock_Sales_Over_Twenty_Thousand_Should_Be_Taxed_At_Fifteen_Percent_Net_Of_Withholding()
    {
        var report = Compute(Buy("PETR4", 1000, 30m, "2026-01-10"), Sell("PETR4", 1000, 40m, "2026-02-10"));

        var feb = report.Single(m => m.Month == 2);
        feb.StockGainsExempt.Should().BeFalse();
        feb[TaxBucket.Common]!.TaxableBase.Should().Be(10_000m);
        feb.TaxDue.Should().Be(1500m);
        feb.Withheld.Should().Be(2m, "0,005% of R$ 40.000");
        feb.WithheldCreditUsed.Should().Be(2m);
        feb.DarfAmount.Should().Be(1498m);
        feb.DarfDeferred.Should().BeFalse();
        feb.DarfDueDate.Should().Be(new DateOnly(2026, 3, 31), "the last business day of the following month");
    }

    [Fact]
    public void The_Ceiling_Is_On_Sales_Not_Gains()
    {
        // R$ 25.000 sold for a R$ 500 gain: taxed, because it is the sales that count.
        var report = Compute(Buy("PETR4", 1000, 24.50m, "2026-01-10"), Sell("PETR4", 1000, 25m, "2026-02-10"));

        var feb = report.Single(m => m.Month == 2);
        feb.StockGainsExempt.Should().BeFalse();
        feb.TaxDue.Should().Be(75m);
    }

    [Fact]
    public void Etf_And_Bdr_Sales_Should_Neither_Count_Towards_Nor_Enjoy_The_Exemption()
    {
        var report = Compute(
            Buy("BOVA11", 100, 100m, "2026-01-10", AssetClass.Etf),
            Sell("BOVA11", 100, 110m, "2026-02-10", AssetClass.Etf));

        var feb = report.Single(m => m.Month == 2);
        feb.StockSales.Should().Be(0m);
        feb[TaxBucket.Common]!.ExemptGain.Should().Be(0m);
        feb.TaxDue.Should().Be(150m);
    }

    [Fact]
    public void A_Loss_In_An_Exempt_Month_Should_Still_Offset_A_Later_Gain()
    {
        var report = Compute(
            Buy("PETR4", 200, 50m, "2026-01-05"),
            Sell("PETR4", 200, 40m, "2026-01-20"),          // −2.000 on R$ 8.000 of sales: exempt month, but a loss
            Buy("VALE3", 1000, 50m, "2026-02-05"),
            Sell("VALE3", 1000, 60m, "2026-02-20"));        // +10.000 on R$ 60.000 of sales

        var jan = report.Single(m => m.Month == 1);
        jan.StockGainsExempt.Should().BeTrue();
        jan[TaxBucket.Common]!.LossCarriedForward.Should().Be(2000m);
        jan.TaxDue.Should().Be(0m);

        var feb = report.Single(m => m.Month == 2);
        feb[TaxBucket.Common]!.LossOffset.Should().Be(2000m);
        feb[TaxBucket.Common]!.TaxableBase.Should().Be(8000m);
        feb.TaxDue.Should().Be(1200m);
        feb[TaxBucket.Common]!.LossCarriedForward.Should().Be(0m);
    }

    [Fact]
    public void Losses_Should_Not_Cross_Buckets()
    {
        var report = Compute(
            Buy("HGLG11", 100, 150m, "2026-01-05", AssetClass.Fii),
            Sell("HGLG11", 100, 100m, "2026-01-20", AssetClass.Fii),   // −5.000 in FIIs
            Buy("VALE3", 1000, 50m, "2026-02-05"),
            Sell("VALE3", 1000, 60m, "2026-02-20"));                   // +10.000 in stocks

        var feb = report.Single(m => m.Month == 2);
        feb[TaxBucket.Common]!.LossOffset.Should().Be(0m);
        feb.TaxDue.Should().Be(1500m);
        feb[TaxBucket.RealEstateFund]!.LossCarriedForward.Should().Be(5000m);
    }

    [Fact]
    public void Day_Trade_Should_Be_Its_Own_Bucket_At_Twenty_Percent_With_One_Percent_Withheld()
    {
        var report = Compute(Buy("PETR4", 100, 10m, "2026-03-10"), Sell("PETR4", 100, 12m, "2026-03-10"));

        var mar = report.Single();
        mar[TaxBucket.DayTrade]!.Result.Should().Be(200m);
        mar[TaxBucket.Common].Should().BeNull("nothing was held overnight");
        mar.TaxDue.Should().Be(40m);
        mar.Withheld.Should().Be(2m);
        mar.DarfAmount.Should().Be(38m);
    }

    [Fact]
    public void A_Day_With_Both_Should_Split_Into_Day_Trade_And_Swing_By_Matched_Quantity()
    {
        var report = Compute(
            Buy("PETR4", 100, 10m, "2026-01-10"),
            Buy("PETR4", 50, 20m, "2026-03-10"),
            Sell("PETR4", 150, 25m, "2026-03-10"));

        var mar = report.Single(m => m.Month == 3);

        // 50 matched today: 50 x (25 − 20). The other 100 came out of the book at R$ 10.
        mar[TaxBucket.DayTrade]!.Result.Should().Be(250m);
        mar[TaxBucket.Common]!.Result.Should().Be(1500m);
        mar[TaxBucket.Common]!.Sales.Should().Be(2500m);
        mar.StockGainsExempt.Should().BeTrue();
        mar.TaxDue.Should().Be(50m, "only the day trade is taxed; the swing gain is exempt");
    }

    [Fact]
    public void Real_Estate_Funds_Should_Pay_Twenty_Percent_With_No_Exemption()
    {
        var report = Compute(
            Buy("HGLG11", 100, 100m, "2026-01-10", AssetClass.Fii),
            Sell("HGLG11", 100, 110m, "2026-02-10", AssetClass.Fii));

        var feb = report.Single(m => m.Month == 2);
        feb[TaxBucket.RealEstateFund]!.TaxableBase.Should().Be(1000m);
        feb.TaxDue.Should().Be(200m);
    }

    [Fact]
    public void Crypto_Should_Be_Exempt_Up_To_Thirty_Five_Thousand_Of_Sales()
    {
        var report = Compute(
            Buy("BTC", 0.5m, 60_000m, "2026-01-10", AssetClass.Crypto),
            Sell("BTC", 0.5m, 68_000m, "2026-02-10", AssetClass.Crypto));

        var feb = report.Single(m => m.Month == 2);
        feb.CryptoSales.Should().Be(34_000m);
        feb.CryptoGainsExempt.Should().BeTrue();
        feb.TaxDue.Should().Be(0m);
    }

    [Fact]
    public void A_Darf_Under_Ten_Reais_Should_Wait_And_Accumulate()
    {
        // Two months each owing R$ 6: neither alone reaches R$ 10, together they do.
        var report = Compute(
            Buy("BOVA11", 10, 100m, "2026-01-05", AssetClass.Etf),
            Sell("BOVA11", 10, 104m, "2026-01-20", AssetClass.Etf),   // +40 → 6,00
            Buy("BOVA11", 10, 100m, "2026-02-05", AssetClass.Etf),
            Sell("BOVA11", 10, 104m, "2026-02-20", AssetClass.Etf));  // +40 → 6,00

        var jan = report.Single(m => m.Month == 1);
        jan.TaxDue.Should().Be(6m);
        jan.DarfDeferred.Should().BeTrue();
        jan.DarfAmount.Should().Be(0m);

        var feb = report.Single(m => m.Month == 2);
        feb.DarfDeferredIn.Should().Be(6m);
        feb.DarfDeferred.Should().BeFalse();
        feb.DarfAmount.Should().Be(12m);
    }

    [Fact]
    public void Unused_Withholding_Should_Carry_To_A_Later_Month_Of_The_Same_Year()
    {
        var report = Compute(
            Buy("PETR4", 1000, 40m, "2026-01-05"),
            Sell("PETR4", 1000, 30m, "2026-01-20"),   // loss on R$ 30.000 of sales: 1,50 withheld, nothing owed
            Buy("VALE3", 1000, 50m, "2026-02-05"),
            Sell("VALE3", 1000, 70m, "2026-02-20"));  // +20.000, 10.000 after the loss → 1.500; 3,50 withheld

        var jan = report.Single(m => m.Month == 1);
        jan.Withheld.Should().Be(1.5m);
        jan.WithheldCreditUsed.Should().Be(0m);

        var feb = report.Single(m => m.Month == 2);
        feb.TaxDue.Should().Be(1500m);
        feb.WithheldCreditUsed.Should().Be(5m, "January's 1,50 plus February's 3,50");
        feb.DarfAmount.Should().Be(1495m);
    }

    [Fact]
    public void A_Split_Should_Be_Applied_Before_The_Sale_Is_Taxed()
    {
        var split = new CorporateAction("MGLU3", CorporateActionKind.Split, new DateOnly(2026, 2, 1), 2m, null, Someone);
        var report = CapitalGains.Compute(
            [Buy("MGLU3", 1000, 30m, "2026-01-10"), Sell("MGLU3", 2000, 20m, "2026-03-10")],
            [split]);

        var mar = report.Single(m => m.Month == 3);
        mar[TaxBucket.Common]!.Result.Should().Be(10_000m, "2000 x (20 − 15)");
        mar.TaxDue.Should().Be(1500m);
    }

    [Fact]
    public void Fees_Should_Reduce_The_Taxable_Gain()
    {
        var report = Compute(
            Buy("PETR4", 1000, 30m, "2026-01-10", fees: 10m),
            Sell("PETR4", 1000, 40m, "2026-02-10", fees: 10m));

        report.Single(m => m.Month == 2)[TaxBucket.Common]!.Result.Should().Be(9980m);
    }

    [Fact]
    public void A_Month_With_Only_Buys_Should_Not_Appear()
    {
        var report = Compute(Buy("PETR4", 100, 30m, "2026-01-10"), Sell("PETR4", 100, 40m, "2026-03-10"));

        report.Select(m => m.Month).Should().Equal(3);
    }

    [Fact]
    public void Fixed_Income_Should_Not_Appear_At_All()
    {
        var report = Compute(
            Buy("CDB-XP", 1, 10_000m, "2026-01-10", AssetClass.FixedIncome),
            Sell("CDB-XP", 1, 11_000m, "2026-02-10", AssetClass.FixedIncome));

        report.Should().BeEmpty("it is taxed at source, by the issuer");
    }

    [Theory]
    [InlineData(2026, 1, 2026, 2, 27)]   // 28 Feb 2026 is a Saturday
    [InlineData(2026, 4, 2026, 5, 29)]   // 31 May 2026 is a Sunday
    [InlineData(2026, 12, 2027, 1, 29)]  // 31 Jan 2027 is a Sunday
    public void The_Darf_Is_Due_On_The_Last_Business_Day_Of_The_Following_Month(int y, int m, int ey, int em, int ed)
    {
        CapitalGains.DarfDueDate(y, m).Should().Be(new DateOnly(ey, em, ed));
    }
}
