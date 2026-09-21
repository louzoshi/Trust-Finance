using TrustFinance.Domain.Entities;

namespace TrustFinance.Domain.Markets;

/// <summary>What one institution holds of the user's money, against what the FGC would return.</summary>
public sealed record FgcExposure(string Issuer, decimal Covered, decimal Uncovered)
{
    public decimal Total => Covered + Uncovered;
    public bool IsOverLimit => Uncovered > 0;
    public decimal Headroom => Math.Max(Fgc.PerInstitutionLimit - Covered, 0m);
}

/// <summary>
/// How much of the book the Fundo Garantidor de Créditos would actually give back.
///
/// The guarantee is R$ 250.000 per holder per institution, counting every covered
/// paper at that institution together, and capped at R$ 1.000.000 across all of them
/// in any four-year window. Splitting R$ 500.000 between two banks is covered;
/// leaving it in one is not, and that is the whole point of computing this.
///
/// Treasury paper is left out: it is not covered because it does not need to be, and
/// counting it as "uncovered" would read as a risk that is not there. Debentures, CRI
/// and CRA are also outside, and there the absence is a real exposure.
/// </summary>
public static class Fgc
{
    public const decimal PerInstitutionLimit = 250_000m;
    public const decimal GlobalLimit = 1_000_000m;

    /// <summary>
    /// Exposure per issuer, largest first. Values are the papers' worth today, since
    /// the guarantee covers principal plus interest accrued up to the intervention.
    /// </summary>
    public static IReadOnlyList<FgcExposure> Exposure(IEnumerable<FixedIncomeValuation> book)
    {
        return
        [
            .. book
                .Where(v => v.Investment.IsOpen && v.Investment.IsFgcCovered)
                .GroupBy(v => v.Investment.Issuer, StringComparer.CurrentCultureIgnoreCase)
                .Select(g =>
                {
                    var total = g.Sum(v => v.OnCurve);
                    var covered = Math.Min(total, PerInstitutionLimit);
                    return new FgcExposure(g.Key, covered, total - covered);
                })
                .OrderByDescending(e => e.Total)
        ];
    }

    /// <summary>The covered total against the four-year ceiling across every institution.</summary>
    public static decimal CoveredTotal(IEnumerable<FgcExposure> exposures)
        => Math.Min(exposures.Sum(e => e.Covered), GlobalLimit);
}
