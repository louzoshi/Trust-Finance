using TrustFinance.Domain.Entities;

namespace TrustFinance.App.Forms;

/// <summary>
/// What the corporate-events form collects. Splits are typed the way the company
/// announces them — "1 para 10" — and turned into the single factor the entity stores.
/// </summary>
public class CorporateActionForm
{
    public string Ticker { get; set; } = string.Empty;
    public CorporateActionKind Kind { get; set; } = CorporateActionKind.Split;
    public DateOnly? Date { get; set; }

    /// <summary>Split: shares after per share before. Reverse: shares before per share after. Bonus: percent.</summary>
    public decimal? Ratio { get; set; }
    public decimal? BonusUnitCost { get; set; }

    public static CorporateActionForm Empty(DateOnly today) => new() { Date = today };

    public static CorporateActionForm From(CorporateAction a) => new()
    {
        Ticker = a.Ticker,
        Kind = a.Kind,
        Date = a.Date,
        Ratio = a.Kind switch
        {
            CorporateActionKind.ReverseSplit => a.Factor > 0 ? Math.Round(1 / a.Factor, 4) : null,
            CorporateActionKind.Bonus => a.Factor * 100m,
            _ => a.Factor
        },
        BonusUnitCost = a.BonusUnitCost
    };

    public CorporateAction ToAction(int userId)
    {
        var ratio = Ratio ?? 0m;
        var factor = Kind switch
        {
            CorporateActionKind.ReverseSplit => ratio > 0 ? 1 / ratio : 0m,
            CorporateActionKind.Bonus => ratio / 100m,
            _ => ratio
        };

        return new CorporateAction(Ticker, Kind, Date ?? default, factor, BonusUnitCost, userId);
    }
}
