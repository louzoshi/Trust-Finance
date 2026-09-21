using TrustFinance.Domain.Entities;

namespace TrustFinance.App;

/// <summary>
/// Portuguese names for the asset classes. They live here and not on the enum because
/// the domain should not know what language the screen speaks — moving countries or
/// adding a locale is then an app-layer change, not a domain one.
/// </summary>
public static class AssetClassNames
{
    public static string Label(this AssetClass c) => c switch
    {
        AssetClass.Stock => "Ações",
        AssetClass.Fii => "FIIs",
        AssetClass.FixedIncome => "Renda fixa",
        AssetClass.Etf => "ETFs",
        AssetClass.Bdr => "BDRs",
        AssetClass.Crypto => "Cripto",
        _ => "Outros"
    };

    public static string Singular(this AssetClass c) => c switch
    {
        AssetClass.Stock => "Ação",
        AssetClass.Fii => "FII",
        AssetClass.FixedIncome => "Renda fixa",
        AssetClass.Etf => "ETF",
        AssetClass.Bdr => "BDR",
        AssetClass.Crypto => "Cripto",
        _ => "Outro"
    };
}
