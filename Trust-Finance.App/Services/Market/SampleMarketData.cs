using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Investing;

namespace TrustFinance.App.Services.Market;

/// <summary>
/// A built-in catalogue of real B3 tickers at plausible prices, so every screen works
/// before anyone signs up for an API token. Prices wobble deterministically from the
/// date: the app looks alive across days without ever inventing a different number for
/// two people looking at the same screen on the same day.
///
/// The numbers are illustrative. The UI says so wherever they are shown — a made-up
/// price that looks like a real one is worse than no price at all.
/// </summary>
public sealed class SampleMarketData(TimeProvider clock) : IMarketDataProvider
{
    public string Name => "Dados de amostra";
    public bool IsLive => false;

    private sealed record Listing(string Ticker, string Name, AssetClass Class, decimal Base, decimal? Yield);

    private static readonly Listing[] Catalogue =
    [
        new("PETR4", "Petrobras PN", AssetClass.Stock, 38.42m, 14.8m),
        new("VALE3", "Vale ON", AssetClass.Stock, 61.20m, 9.3m),
        new("ITUB4", "Itaú Unibanco PN", AssetClass.Stock, 34.75m, 6.1m),
        new("BBDC4", "Bradesco PN", AssetClass.Stock, 14.28m, 7.4m),
        new("BBAS3", "Banco do Brasil ON", AssetClass.Stock, 27.90m, 10.2m),
        new("ABEV3", "Ambev ON", AssetClass.Stock, 12.65m, 5.5m),
        new("WEGE3", "WEG ON", AssetClass.Stock, 52.10m, 1.6m),
        new("B3SA3", "B3 ON", AssetClass.Stock, 11.84m, 5.9m),
        new("MGLU3", "Magazine Luiza ON", AssetClass.Stock, 8.36m, null),
        new("SUZB3", "Suzano ON", AssetClass.Stock, 56.40m, 2.1m),
        new("RENT3", "Localiza ON", AssetClass.Stock, 39.75m, 1.9m),
        new("PRIO3", "PRIO ON", AssetClass.Stock, 42.30m, null),

        new("HGLG11", "CSHG Logística", AssetClass.Fii, 158.90m, 8.4m),
        new("MXRF11", "Maxi Renda", AssetClass.Fii, 10.42m, 11.2m),
        new("KNRI11", "Kinea Renda Imobiliária", AssetClass.Fii, 152.30m, 8.1m),
        new("XPML11", "XP Malls", AssetClass.Fii, 108.75m, 9.0m),
        new("VISC11", "Vinci Shopping Centers", AssetClass.Fii, 104.20m, 9.6m),
        new("BTLG11", "BTG Logística", AssetClass.Fii, 99.80m, 8.8m),
        new("HGRU11", "CSHG Renda Urbana", AssetClass.Fii, 121.45m, 8.7m),

        new("IVVB11", "iShares S&P 500", AssetClass.Etf, 382.60m, null),
        new("BOVA11", "iShares Ibovespa", AssetClass.Etf, 128.35m, 4.2m),
        new("SMAL11", "iShares Small Caps", AssetClass.Etf, 21.90m, 3.1m),
        new("IMAB11", "IMA-B — inflação", AssetClass.Etf, 92.15m, 6.3m),

        new("AAPL34", "Apple BDR", AssetClass.Bdr, 62.40m, 0.5m),
        new("MSFT34", "Microsoft BDR", AssetClass.Bdr, 78.20m, 0.7m),
        new("GOGL34", "Alphabet BDR", AssetClass.Bdr, 54.90m, null),
        new("AMZO34", "Amazon BDR", AssetClass.Bdr, 48.65m, null),
        new("NVDC34", "NVIDIA BDR", AssetClass.Bdr, 71.30m, null),

        new("TESOURO-SELIC-2029", "Tesouro Selic 2029", AssetClass.FixedIncome, 15_420.18m, 14.9m),
        new("TESOURO-IPCA-2035", "Tesouro IPCA+ 2035", AssetClass.FixedIncome, 3_182.44m, 6.8m),
        new("TESOURO-PRE-2028", "Tesouro Prefixado 2028", AssetClass.FixedIncome, 742.90m, 13.2m),
        new("CDB-LIQ-DIARIA", "CDB Liquidez Diária 102% CDI", AssetClass.FixedIncome, 1_000.00m, 15.2m),

        new("BTC", "Bitcoin", AssetClass.Crypto, 512_400.00m, null),
        new("ETH", "Ethereum", AssetClass.Crypto, 18_260.00m, null),
        new("SOL", "Solana", AssetClass.Crypto, 1_145.00m, null),
        new("USDT", "Tether", AssetClass.Crypto, 5.42m, null)
    ];

    private static readonly Dictionary<string, Listing> ByTicker =
        Catalogue.ToDictionary(l => l.Ticker, StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyDictionary<string, Quote>> GetQuotesAsync(
        IEnumerable<string> tickers, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

        var quotes = new Dictionary<string, Quote>(StringComparer.OrdinalIgnoreCase);
        foreach (var ticker in tickers)
        {
            if (ByTicker.TryGetValue(ticker, out var listing))
                quotes[ticker] = ToQuote(listing, today);
        }

        return Task.FromResult<IReadOnlyDictionary<string, Quote>>(quotes);
    }

    public Task<IReadOnlyList<Quote>> BrowseAsync(
        AssetClass? assetClass, string? search, int limit = 40, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

        var matches = Catalogue.AsEnumerable();

        if (assetClass is { } c)
            matches = matches.Where(l => l.Class == c);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            matches = matches.Where(l =>
                l.Ticker.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                l.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var results = matches
            .Select(l => ToQuote(l, today))
            .OrderBy(q => q.Ticker, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<Quote>>(results);
    }

    public Task<MarketIndicators> GetIndicatorsAsync(CancellationToken ct = default) =>
        Task.FromResult(Fallback(clock.GetLocalNow().UtcDateTime));

    /// <summary>Plausible Brazilian rates, used offline and whenever the live call fails.</summary>
    public static MarketIndicators Fallback(DateTime now) => new(
        CdiAnnual: 14.90m,
        SelicAnnual: 15.00m,
        IpcaAnnual: 4.52m,
        UpdatedAt: now);

    private Quote ToQuote(Listing listing, DateOnly today)
    {
        // A stable pseudo-random move per ticker per day: same screen, same number, all day.
        var seed = HashCode.Combine(listing.Ticker, today.DayNumber);
        var swing = listing.Class switch
        {
            AssetClass.Crypto => 6.0,
            AssetClass.FixedIncome => 0.15,
            AssetClass.Fii => 1.2,
            _ => 2.5
        };

        var percent = (decimal)((seed % 2001 - 1000) / 1000.0 * swing);
        var previousClose = listing.Base;
        var price = Math.Round(previousClose * (1 + percent / 100m), listing.Class == AssetClass.Crypto ? 2 : 2);

        return new Quote(
            listing.Ticker,
            listing.Name,
            listing.Class,
            price,
            previousClose,
            listing.Yield,
            UpdatedAt: clock.GetLocalNow().UtcDateTime);
    }
}
