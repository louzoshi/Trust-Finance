using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Investing;

namespace TrustFinance.App.Services.Market;

/// <summary>
/// Where prices come from. Two implementations ship: one that talks to brapi.dev and one
/// that answers from a built-in catalogue. The app is usable the moment it is installed
/// because the offline one is the default, and becomes live when a token is saved.
/// </summary>
public interface IMarketDataProvider
{
    /// <summary>Shown in the UI so it is always obvious whether a number is real.</summary>
    string Name { get; }

    bool IsLive { get; }

    /// <summary>
    /// Prices for the given tickers, keyed case-insensitively. Tickers the provider does
    /// not know are simply absent — a caller must handle a missing quote anyway, since
    /// the network can fail.
    /// </summary>
    Task<IReadOnlyDictionary<string, Quote>> GetQuotesAsync(
        IEnumerable<string> tickers, CancellationToken ct = default);

    /// <summary>The catalogue to browse when looking for something to buy.</summary>
    Task<IReadOnlyList<Quote>> BrowseAsync(
        AssetClass? assetClass, string? search, int limit = 40, CancellationToken ct = default);

    /// <summary>CDI, SELIC and IPCA — what a return gets compared against.</summary>
    Task<MarketIndicators> GetIndicatorsAsync(CancellationToken ct = default);
}
