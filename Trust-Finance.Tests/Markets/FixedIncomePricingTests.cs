using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Markets;

namespace TrustFinance.Tests.Markets;

public class FixedIncomePricingTests
{
    private const int Someone = 1;

    /// <summary>A year and a bit after the purchase, so the regressive table has moved on.</summary>
    private static readonly DateOnly Today = new(2026, 9, 21);

    private static FixedIncomeInvestment Paper(
        FixedIncomeKind kind = FixedIncomeKind.Cdb,
        IndexKind index = IndexKind.Fixed,
        decimal rate = 12m,
        decimal principal = 10_000m,
        string purchase = "2025-09-22",
        string maturity = "2028-09-22",
        string issuer = "Banco Exemplo")
        => new(issuer, kind, index, rate, principal, DateOnly.Parse(purchase), DateOnly.Parse(maturity), Someone);

    private static List<DailyRate> FlatSeries(string from, string to, decimal dailyPercent)
    {
        var rates = new List<DailyRate>();
        for (var d = DateOnly.Parse(from); d <= DateOnly.Parse(to); d = d.AddDays(1))
            if (BusinessCalendar.IsBusinessDay(d))
                rates.Add(new DailyRate(d, dailyPercent));

        return rates;
    }

    [Fact]
    public void A_Prefixado_Should_Accrue_Its_Own_Rate_Over_Business_Days()
    {
        var paper = Paper();
        var value = FixedIncomePricing.Value(paper, Today);

        var expected = 10_000m * Rates.Factor(12m, value.ElapsedBusinessDays);

        value.OnCurve.Should().BeApproximately(Math.Round(expected, 2), 0.01m);
        value.OnCurve.Should().BeGreaterThan(10_000m);
        value.ElapsedBusinessDays.Should().Be(BusinessCalendar.Count(new DateOnly(2025, 9, 22), Today));
    }

    [Fact]
    public void A_Percentage_Of_The_Cdi_Should_Follow_The_Published_Series()
    {
        var paper = Paper(index: IndexKind.PercentOfCdi, rate: 110m);
        var cdi = FlatSeries("2025-09-22", "2026-09-21", 0.05m);

        var value = FixedIncomePricing.Value(paper, Today, cdi);

        var window = cdi.Where(r => r.Date > paper.PurchaseDate && r.Date <= Today).ToList();
        var expected = 10_000m * Rates.PercentOfCdiFactor(window, 110m);

        value.OnCurve.Should().BeApproximately(Math.Round(expected, 2), 0.01m);
    }

    [Fact]
    public void Without_The_Index_Series_A_Post_Fixed_Paper_Should_Sit_At_Its_Principal()
    {
        var value = FixedIncomePricing.Value(Paper(index: IndexKind.PercentOfCdi, rate: 110m), Today);

        value.OnCurve.Should().Be(10_000m, "inventing an accrual nobody published would be worse than showing cost");
    }

    [Fact]
    public void A_Cdi_Plus_Spread_Should_Compound_Both()
    {
        var paper = Paper(index: IndexKind.CdiPlus, rate: 2m);
        var cdi = FlatSeries("2025-09-22", "2026-09-21", 0.05m);

        var value = FixedIncomePricing.Value(paper, Today, cdi);
        var onlyCdi = FixedIncomePricing.Value(Paper(index: IndexKind.CdiPlus, rate: 0m), Today, cdi);

        value.OnCurve.Should().BeGreaterThan(onlyCdi.OnCurve);
    }

    [Fact]
    public void A_Prefixado_Should_Be_Worth_Less_Than_Its_Curve_When_Rates_Have_Risen()
    {
        // Bought at 12%, the market now charges 16% for the remaining term.
        var paper = Paper(rate: 12m);
        var value = FixedIncomePricing.Value(paper, Today, null, YieldCurve.Flat(16m));

        value.MarketRate.Should().Be(16m);
        value.MarketValue.Should().BeLessThan(value.OnCurve);
        value.IsUnderwater.Should().BeTrue();
        value.MarkToMarketGap.Should().BeNegative();
    }

    [Fact]
    public void A_Prefixado_Should_Be_Worth_More_Than_Its_Curve_When_Rates_Have_Fallen()
    {
        var value = FixedIncomePricing.Value(Paper(rate: 12m), Today, null, YieldCurve.Flat(8m));

        value.MarketValue.Should().BeGreaterThan(value.OnCurve);
        value.IsUnderwater.Should().BeFalse();
    }

    [Fact]
    public void Marking_At_The_Contracted_Rate_Should_Return_The_Curve()
    {
        var value = FixedIncomePricing.Value(Paper(rate: 12m), Today, null, YieldCurve.Flat(12m));

        value.MarketValue.Should().BeApproximately(value.OnCurve, 0.05m,
            "discounting at the rate it was bought at is the curve, by definition");
    }

    [Fact]
    public void An_Ipca_Paper_Should_Not_Be_Marked_Against_A_Nominal_Curve()
    {
        var ipca = Paper(FixedIncomeKind.TesouroIpca, IndexKind.IpcaPlus, rate: 6m);

        var value = FixedIncomePricing.Value(ipca, Today, null, YieldCurve.Flat(15m));

        value.MarketRate.Should().BeNull();
        value.MarketValue.Should().Be(value.OnCurve,
            "its price comes from the real curve; discounting a real flow at a nominal rate is not a mark, it is a category error");
    }

    [Fact]
    public void A_Post_Fixed_Paper_Should_Not_Be_Marked_To_Market()
    {
        var cdi = FlatSeries("2025-09-22", "2026-09-21", 0.05m);
        var value = FixedIncomePricing.Value(Paper(index: IndexKind.PercentOfCdi, rate: 110m), Today, cdi, YieldCurve.Flat(20m));

        value.MarketRate.Should().BeNull();
        value.MarketValue.Should().Be(value.OnCurve, "its rate resets daily, so there is no duration to lose");
    }

    [Fact]
    public void At_Maturity_There_Is_Nothing_Left_To_Discount()
    {
        var paper = Paper(maturity: "2026-09-21");
        var value = FixedIncomePricing.Value(paper, Today, null, YieldCurve.Flat(20m));

        value.RemainingBusinessDays.Should().Be(0);
        value.MarketValue.Should().Be(value.OnCurve);
    }

    [Fact]
    public void Accrual_Should_Stop_At_Maturity_Not_Run_Past_It()
    {
        var matured = FixedIncomePricing.Value(Paper(maturity: "2026-03-20"), Today);
        var atMaturity = FixedIncomePricing.Value(Paper(maturity: "2026-03-20"), new DateOnly(2026, 3, 20));

        matured.OnCurve.Should().Be(atMaturity.OnCurve, "a matured paper stopped earning the day it matured");
    }

    [Fact]
    public void Accrual_Should_Stop_On_Redemption()
    {
        var paper = Paper();
        paper.Redeem(new DateOnly(2026, 3, 20));

        var value = FixedIncomePricing.Value(paper, Today);

        value.OnCurve.Should().Be(FixedIncomePricing.Value(Paper(), new DateOnly(2026, 3, 20)).OnCurve);
    }

    [Fact]
    public void A_Cdb_Should_Report_What_Is_Left_After_The_Regressive_Table()
    {
        var value = FixedIncomePricing.Value(Paper(), Today);

        value.ElapsedCalendarDays.Should().Be(364);
        value.Tax.IncomeTaxRate.Should().Be(0.175m, "past 360 days the table has already stepped down to 17,5%");
        value.NetValue.Should().BeLessThan(value.OnCurve);
        value.NetValue.Should().Be(10_000m + value.Tax.Net);
    }

    [Fact]
    public void An_Lci_Should_Keep_Its_Whole_Gain()
    {
        var lci = FixedIncomePricing.Value(Paper(FixedIncomeKind.Lci), Today);
        var cdb = FixedIncomePricing.Value(Paper(FixedIncomeKind.Cdb), Today);

        lci.Tax.Total.Should().Be(0m);
        lci.NetValue.Should().BeGreaterThan(cdb.NetValue, "same rate, and the LCI pays no tax");
    }

    [Fact]
    public void An_Lci_At_A_Lower_Rate_Can_Still_Beat_A_Cdb()
    {
        var lci = FixedIncomePricing.Value(Paper(FixedIncomeKind.Lci, rate: 10m), Today);
        var cdb = FixedIncomePricing.Value(Paper(FixedIncomeKind.Cdb, rate: 12m), Today);

        lci.OnCurve.Should().BeLessThan(cdb.OnCurve);
        lci.NetValue.Should().BeGreaterThan(cdb.NetValue, "the exemption is worth more than the two points");
    }

    [Fact]
    public void Treasury_Paper_Should_Pay_Custody_And_Tesouro_Selic_Only_Above_Ten_Thousand()
    {
        var prefixado = FixedIncomePricing.Value(Paper(FixedIncomeKind.TesouroPrefixado, principal: 10_000m), Today);
        var selicSmall = FixedIncomePricing.Value(Paper(FixedIncomeKind.TesouroSelic, principal: 10_000m), Today);
        var selicBig = FixedIncomePricing.Value(Paper(FixedIncomeKind.TesouroSelic, principal: 30_000m), Today);

        prefixado.CustodyFee.Should().BeApproximately(10_000m * 0.002m * prefixado.ElapsedBusinessDays / 252m, 0.01m);
        selicSmall.CustodyFee.Should().Be(0m, "the first R$ 10.000 of Tesouro Selic is free");
        selicBig.CustodyFee.Should().BeApproximately(20_000m * 0.002m * selicBig.ElapsedBusinessDays / 252m, 0.01m);
    }

    [Fact]
    public void A_Cdb_Should_Pay_No_Custody()
    {
        FixedIncomePricing.Value(Paper(), Today).CustodyFee.Should().Be(0m);
    }

    [Fact]
    public void The_Net_Annual_Rate_Should_Be_Below_The_Contracted_One()
    {
        var value = FixedIncomePricing.Value(Paper(rate: 12m), Today);

        value.NetAnnualRate.Should().BeLessThan(12m).And.BeGreaterThan(9m,
            "a fifth of the gain goes to the Receita, so a 12% CDB pays about 9,6% in the hand");
    }

    [Fact]
    public void A_Book_Should_Come_Back_Ordered_By_Maturity()
    {
        var book = FixedIncomePricing.ValueAll(
        [
            Paper(maturity: "2029-01-10"),
            Paper(maturity: "2027-01-10"),
            Paper(maturity: "2028-01-10")
        ], Today);

        book.Select(v => v.Investment.MaturityDate.Year).Should().Equal(2027, 2028, 2029);
    }
}
