using TrustFinance.Domain.Entities;

namespace TrustFinance.Domain.Markets;

/// <summary>What one paper is worth today, by both of the two answers that exist.</summary>
public sealed record FixedIncomeValuation(
    FixedIncomeInvestment Investment,
    /// <summary>Business days accrued so far.</summary>
    int ElapsedBusinessDays,
    int ElapsedCalendarDays,
    int RemainingBusinessDays,
    /// <summary>Held to maturity: principal plus what the contract has earned. Never falls.</summary>
    decimal OnCurve,
    /// <summary>Sold today: the remaining flow discounted at today's rate. Falls when rates rise.</summary>
    decimal MarketValue,
    /// <summary>The rate the market is charging for the remaining term, when it was marked.</summary>
    decimal? MarketRate,
    TaxBreakdown Tax,
    decimal CustodyFee)
{
    /// <summary>The accrued gain before tax, on the curve.</summary>
    public decimal GrossGain => OnCurve - Investment.Principal;

    /// <summary>What redeeming today would actually put in the account.</summary>
    public decimal NetValue => Investment.Principal + Tax.Net - CustodyFee;

    /// <summary>
    /// The difference between the two marks. Negative means the paper is worth less than
    /// it cost so far — rates rose since the purchase, and selling early would realise it.
    /// </summary>
    public decimal MarkToMarketGap => MarketValue - OnCurve;

    /// <summary>True when selling today would hand back less than holding to maturity.</summary>
    public bool IsUnderwater => Investment.IsMarkedToMarket && MarkToMarketGap < 0;

    public decimal? NetAnnualRate => ElapsedBusinessDays > 0 && Investment.Principal > 0
        ? Rates.AnnualFrom(NetValue / Investment.Principal, ElapsedBusinessDays)
        : null;
}

/// <summary>
/// Prices the fixed-income book: how much each paper has earned, and what it would
/// fetch if sold today.
///
/// Two numbers, because fixed income has two honest answers and confusing them is the
/// classic mistake. <b>Na curva</b> is the contract: principal compounded at the agreed
/// rate, which only ever rises and is what the holder gets at maturity. <b>A mercado</b>
/// is what someone would pay for the remaining flow at today's rates — it falls when
/// rates rise, and it is the only number that matters if the paper is sold early.
///
/// For post-fixed paper the two coincide: the rate resets daily, so there is no duration
/// to lose. A prefixado is where a paper can be deeply underwater and its owner never
/// know, because the statement quotes the curve. IPCA+ moves too, but against the real
/// curve — see <see cref="FixedIncomeInvestment.IsMarkedToMarket"/> for why it is left
/// on its curve rather than marked against a nominal one.
/// </summary>
public static class FixedIncomePricing
{
    /// <summary>B3's custody fee on Tesouro Direto, charged on the position, per year.</summary>
    public const decimal TreasuryCustodyRate = 0.0020m;

    /// <summary>Tesouro Selic pays no custody on the first slice of the position.</summary>
    public const decimal TreasurySelicCustodyFreeAmount = 10_000m;

    /// <summary>
    /// Values one paper.
    /// </summary>
    /// <param name="index">
    /// The daily series the paper follows — CDI, SELIC or IPCA, as the Banco Central
    /// publishes it. Only the days between purchase and today are used. A prefixado
    /// ignores it entirely.
    /// </param>
    /// <param name="curve">Today's term structure, for the mark to market. Null leaves the paper at its curve.</param>
    public static FixedIncomeValuation Value(
        FixedIncomeInvestment paper,
        DateOnly today,
        IReadOnlyList<DailyRate>? index = null,
        YieldCurve? curve = null)
    {
        var end = paper.AccrualEnd(today);
        var elapsedBusiness = BusinessCalendar.Count(paper.PurchaseDate, end);
        var elapsedCalendar = Math.Max(end.DayNumber - paper.PurchaseDate.DayNumber, 0);
        var remaining = BusinessCalendar.Count(end, paper.MaturityDate);

        var onCurve = Round(paper.Principal * AccrualFactor(paper, elapsedBusiness, index, end));

        // Marking to market means asking what the remaining flow is worth now. Only a
        // paper whose future is fixed has a flow to discount; a post-fixed one resets,
        // so its curve value is already its market value.
        decimal marketValue = onCurve;
        decimal? marketRate = null;

        if (paper.IsMarkedToMarket && curve is not null && remaining > 0 && paper.IsOpen)
        {
            marketRate = curve.RateFor(remaining);
            var atMaturity = paper.Principal * AccrualFactor(paper, BusinessCalendar.Count(paper.PurchaseDate, paper.MaturityDate), index, paper.MaturityDate);
            marketValue = Round(atMaturity * curve.DiscountFactor(remaining));
        }

        var custody = Round(CustodyFee(paper, elapsedBusiness));
        var tax = FixedIncomeTax.On(onCurve - paper.Principal, elapsedCalendar, paper.IsTaxExempt);

        return new FixedIncomeValuation(
            paper, elapsedBusiness, elapsedCalendar, remaining,
            onCurve, marketValue, marketRate, tax, custody);
    }

    /// <summary>Every open paper, valued, newest maturity last.</summary>
    public static IReadOnlyList<FixedIncomeValuation> ValueAll(
        IEnumerable<FixedIncomeInvestment> papers,
        DateOnly today,
        IReadOnlyList<DailyRate>? cdi = null,
        IReadOnlyList<DailyRate>? selic = null,
        IReadOnlyList<DailyRate>? ipca = null,
        YieldCurve? curve = null)
    {
        return
        [
            .. papers
                .Select(p => Value(p, today, SeriesFor(p, cdi, selic, ipca), curve))
                .OrderBy(v => v.Investment.MaturityDate)
                .ThenBy(v => v.Investment.Id)
        ];
    }

    private static IReadOnlyList<DailyRate>? SeriesFor(
        FixedIncomeInvestment paper,
        IReadOnlyList<DailyRate>? cdi,
        IReadOnlyList<DailyRate>? selic,
        IReadOnlyList<DailyRate>? ipca) => paper.Index switch
    {
        IndexKind.PercentOfCdi or IndexKind.CdiPlus => cdi,
        IndexKind.SelicPlus => selic,
        IndexKind.IpcaPlus => ipca,
        _ => null
    };

    /// <summary>
    /// What one unit of principal has grown to. A prefixado compounds its own rate over
    /// the business days; an indexed paper compounds the published index and, where
    /// there is a spread, the spread alongside it.
    /// </summary>
    public static decimal AccrualFactor(
        FixedIncomeInvestment paper,
        int businessDays,
        IReadOnlyList<DailyRate>? index,
        DateOnly end)
    {
        if (businessDays <= 0)
            return 1m;

        var window = index?.Where(r => r.Date > paper.PurchaseDate && r.Date <= end).ToList();

        return paper.Index switch
        {
            IndexKind.Fixed => Rates.Factor(paper.Rate, businessDays),

            // Without the series there is nothing honest to accrue by, so the paper sits
            // at principal rather than at a number nobody published.
            IndexKind.PercentOfCdi => window is { Count: > 0 }
                ? Rates.PercentOfCdiFactor(window, paper.Rate)
                : 1m,

            IndexKind.CdiPlus or IndexKind.SelicPlus => window is { Count: > 0 }
                ? Rates.IndexFactor(window) * Rates.Factor(paper.Rate, businessDays)
                : Rates.Factor(paper.Rate, businessDays),

            // IPCA is published monthly, so the series carries monthly figures; the real
            // rate compounds over business days alongside it.
            IndexKind.IpcaPlus => (window is { Count: > 0 } ? Rates.IndexFactor(window) : 1m)
                                  * Rates.Factor(paper.Rate, businessDays),

            _ => 1m
        };
    }

    /// <summary>
    /// B3 charges 0,20% a year on a Tesouro position, pro rata over the days held.
    /// Tesouro Selic is free on its first R$ 10.000, which is why it is the parking spot
    /// for an emergency reserve.
    /// </summary>
    public static decimal CustodyFee(FixedIncomeInvestment paper, int businessDays)
    {
        if (!paper.Kind.IsTreasury() || businessDays <= 0)
            return 0m;

        var chargeable = paper.Kind == FixedIncomeKind.TesouroSelic
            ? Math.Max(paper.Principal - TreasurySelicCustodyFreeAmount, 0m)
            : paper.Principal;

        return chargeable * TreasuryCustodyRate * businessDays / BusinessCalendar.YearInBusinessDays;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.ToEven);
}
