using TrustFinance.Domain.Entities;

namespace TrustFinance.App.Services.Market;

/// <summary>Somewhere an asset of a given class can actually be bought.</summary>
public sealed record Broker(string Name, string Url, string Note);

/// <summary>
/// Reference data for the "where do I buy this" question. Everything listed on B3 trades
/// through any broker with a B3 account, so the list is by asset class rather than by
/// ticker. This is a directory, not a recommendation, and the order is alphabetical on
/// purpose so it does not read as a ranking.
/// </summary>
public static class Brokers
{
    private static readonly Broker[] B3 =
    [
        new("Ágora", "https://www.agorainvestimentos.com.br", "Corretora do Bradesco"),
        new("BTG Pactual", "https://www.btgpactualdigital.com", "Banco de investimento"),
        new("Clear", "https://www.clear.com.br", "Corretagem zero em ações"),
        new("Inter", "https://www.bancointer.com.br", "Banco digital com corretora"),
        new("Itaú Corretora", "https://www.itau.com.br/investimentos", "Corretora do Itaú"),
        new("NuInvest", "https://www.nuinvest.com.br", "Corretora do Nubank"),
        new("Rico", "https://www.rico.com.vc", "Corretora do grupo XP"),
        new("XP Investimentos", "https://www.xpi.com.br", "Maior corretora independente do país")
    ];

    private static readonly Broker[] Crypto =
    [
        new("Binance", "https://www.binance.com/pt-BR", "Maior exchange global"),
        new("Bitso", "https://bitso.com/pt", "Exchange com operação no Brasil"),
        new("Foxbit", "https://foxbit.com.br", "Exchange brasileira"),
        new("Mercado Bitcoin", "https://www.mercadobitcoin.com.br", "Maior exchange brasileira")
    ];

    private static readonly Broker[] FixedIncome =
    [
        new("Tesouro Direto", "https://www.tesourodireto.com.br", "Títulos públicos, direto do Tesouro"),
        new("BTG Pactual", "https://www.btgpactualdigital.com", "CDB, LCI, LCA e debêntures"),
        new("Inter", "https://www.bancointer.com.br", "CDB e LCI próprios"),
        new("XP Investimentos", "https://www.xpi.com.br", "Vitrine ampla de emissores")
    ];

    public static IReadOnlyList<Broker> For(AssetClass assetClass) => assetClass switch
    {
        AssetClass.Crypto => Crypto,
        AssetClass.FixedIncome => FixedIncome,
        _ => B3
    };

    /// <summary>Where the asset is traded, for the line above the broker list.</summary>
    public static string Venue(AssetClass assetClass) => assetClass switch
    {
        AssetClass.Crypto => "Exchanges de criptomoedas",
        AssetClass.FixedIncome => "Tesouro Direto e mesas de renda fixa",
        _ => "B3 — Bolsa de Valores de São Paulo"
    };
}
