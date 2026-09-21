using TrustFinance.Domain.Entities;

namespace TrustFinance.App.Forms;

/// <summary>What the fixed income screen collects. The rules live on <see cref="FixedIncomeInvestment"/>.</summary>
public class FixedIncomeForm
{
    public string Issuer { get; set; } = string.Empty;
    public FixedIncomeKind Kind { get; set; } = FixedIncomeKind.Cdb;
    public IndexKind Index { get; set; } = IndexKind.PercentOfCdi;
    public decimal? Rate { get; set; }
    public decimal? Principal { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? MaturityDate { get; set; }
    public bool HasDailyLiquidity { get; set; }
    public string? Note { get; set; }

    public static FixedIncomeForm Empty(DateOnly today) => new()
    {
        PurchaseDate = today,
        MaturityDate = today.AddYears(1),
        Rate = 100m
    };

    public static FixedIncomeForm From(FixedIncomeInvestment f) => new()
    {
        Issuer = f.Issuer,
        Kind = f.Kind,
        Index = f.Index,
        Rate = f.Rate,
        Principal = f.Principal,
        PurchaseDate = f.PurchaseDate,
        MaturityDate = f.MaturityDate,
        HasDailyLiquidity = f.HasDailyLiquidity,
        Note = f.Note
    };

    /// <summary>What the rate field means for the chosen index, so the label can say it.</summary>
    public string RateLabel => Index switch
    {
        IndexKind.PercentOfCdi => "% do CDI",
        IndexKind.CdiPlus => "CDI + (% a.a.)",
        IndexKind.IpcaPlus => "IPCA + (% a.a.)",
        IndexKind.SelicPlus => "Selic + (% a.a.)",
        _ => "Taxa (% a.a.)"
    };

    public FixedIncomeInvestment ToInvestment(int userId) => new(
        Issuer,
        Kind,
        Index,
        Rate ?? 0m,
        Principal ?? 0m,
        PurchaseDate ?? default,
        MaturityDate ?? default,
        userId,
        HasDailyLiquidity,
        Note);
}
