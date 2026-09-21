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

    /// <summary>"1 para 10", "10 para 1", "10%".</summary>
    public static string Describe(this CorporateAction a) => a.Kind switch
    {
        CorporateActionKind.Split => $"1 para {a.Factor:0.####}",
        CorporateActionKind.ReverseSplit => $"{(a.Factor > 0 ? 1 / a.Factor : 0):0.####} para 1",
        CorporateActionKind.Bonus => $"{a.Factor * 100m:0.##}% a {Money.Format(a.BonusUnitCost ?? 0m)}",
        _ => "—"
    };
}
