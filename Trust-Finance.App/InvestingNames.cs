using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Tax;

namespace TrustFinance.App;

/// <summary>Screen names for the investing enums, kept out of the domain like the asset class labels.</summary>
public static class InvestingNames
{
    public static string Label(this PayoutKind k) => k switch
    {
        PayoutKind.Dividend => "Dividendo",
        PayoutKind.InterestOnEquity => "JCP",
        PayoutKind.FundIncome => "Rendimento",
        _ => "—"
    };

    public static string Label(this CorporateActionKind k) => k switch
    {
        CorporateActionKind.Split => "Desdobramento",
        CorporateActionKind.ReverseSplit => "Grupamento",
        CorporateActionKind.Bonus => "Bonificação",
        _ => "—"
    };

    public static string Label(this TaxBucket b) => b switch
    {
        TaxBucket.Common => "Operações comuns",
        TaxBucket.DayTrade => "Day trade",
        TaxBucket.RealEstateFund => "FIIs",
        TaxBucket.Crypto => "Cripto",
        _ => "—"
    };

    public static string Label(this FixedIncomeKind k) => k switch
    {
        FixedIncomeKind.Cdb => "CDB",
        FixedIncomeKind.Lci => "LCI",
        FixedIncomeKind.Lca => "LCA",
        FixedIncomeKind.Lc => "LC",
        FixedIncomeKind.TesouroSelic => "Tesouro Selic",
        FixedIncomeKind.TesouroPrefixado => "Tesouro Prefixado",
        FixedIncomeKind.TesouroIpca => "Tesouro IPCA+",
        FixedIncomeKind.Debenture => "Debênture",
        FixedIncomeKind.DebentureIncentivada => "Debênture incentivada",
        FixedIncomeKind.Cri => "CRI",
        FixedIncomeKind.Cra => "CRA",
        FixedIncomeKind.Poupanca => "Poupança",
        _ => "—"
    };

    public static string Label(this IndexKind i) => i switch
    {
        IndexKind.Fixed => "Prefixado",
        IndexKind.PercentOfCdi => "% do CDI",
        IndexKind.CdiPlus => "CDI +",
        IndexKind.IpcaPlus => "IPCA +",
        IndexKind.SelicPlus => "Selic +",
        _ => "—"
    };

    /// <summary>"110% do CDI", "IPCA + 6,00%", "12,50% a.a."</summary>
    public static string Describe(this FixedIncomeInvestment f) => f.Index switch
    {
        IndexKind.PercentOfCdi => $"{f.Rate:0.##}% do CDI",
        IndexKind.CdiPlus => $"CDI + {f.Rate:0.00}%",
        IndexKind.IpcaPlus => $"IPCA + {f.Rate:0.00}%",
        IndexKind.SelicPlus => $"Selic + {f.Rate:0.00}%",
        _ => $"{f.Rate:0.00}% a.a."
    };

    /// <summary>"1 para 10", "10 para 1", "10%".</summary>
    public static string Describe(this CorporateAction a) => a.Kind switch
    {
        CorporateActionKind.Split => $"1 para {a.Factor:0.####}",
        CorporateActionKind.ReverseSplit => $"{(a.Factor > 0 ? 1 / a.Factor : 0):0.####} para 1",
        CorporateActionKind.Bonus => $"{a.Factor * 100m:0.##}% a {Money.Format(a.BonusUnitCost ?? 0m)}",
        _ => "—"
    };
}
