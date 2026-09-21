using Microsoft.Extensions.Caching.Memory;
using TrustFinance.App.Services;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Investing;

namespace TrustFinance.App.Services.Market;

/// <summary>
/// The one place the rest of the app asks for prices. Picks the provider from the user's
/// saved token and caches the answers, because the free market-data tiers are rate
/// limited and a Blazor page can re-render far more often than a price actually moves.
/// </summary>
public class MarketData(
    IHttpClientFactory httpFactory,
    SettingsService settings,
    IMemoryCache cache,
    TimeProvider clock,
    ILoggerFactory loggers)
{
    public async Task<IMarketDataProvider> ProviderAsync(int userId)
    {
        var user = await settings.GetAsync(userId);
        return Build(user.MarketDataToken);
    }

    private IMarketDataProvider Build(string? token) =>
        string.IsNullOrWhiteSpace(token)
            ? new SampleMarketData(clock)
            : new BrapiMarketData(
                httpFactory.CreateClient(nameof(BrapiMarketData)),
                token,
                loggers.CreateLogger<BrapiMarketData>());

    public async Task<IReadOnlyDictionary<string, Quote>> QuotesAsync(int userId, IReadOnlyList<string> tickers)
    {
        if (tickers.Count == 0)
            return new Dictionary<string, Quote>(StringComparer.OrdinalIgnoreCase);

        var user = await settings.GetAsync(userId);
        var provider = Build(user.MarketDataToken);

        var key = $"quotes:{userId}:{string.Join(',', tickers.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))}";

        if (cache.TryGetValue(key, out IReadOnlyDictionary<string, Quote>? cached) && cached is not null)
            return cached;

        var quotes = await provider.GetQuotesAsync(tickers);
        cache.Set(key, quotes, TimeSpan.FromMinutes(user.QuoteRefreshMinutes));
        return quotes;
    }

    public async Task<IReadOnlyList<Quote>> BrowseAsync(int userId, AssetClass? assetClass, string? search)
    {
        var user = await settings.GetAsync(userId);
        var provider = Build(user.MarketDataToken);

        // A search is typed a character at a time; caching it would mostly cache misses.
        if (!string.IsNullOrWhiteSpace(search))
            return await provider.BrowseAsync(assetClass, search);

        var key = $"browse:{userId}:{assetClass}";
        if (cache.TryGetValue(key, out IReadOnlyList<Quote>? cached) && cached is not null)
            return cached;

        var results = await provider.BrowseAsync(assetClass, null);
        cache.Set(key, results, TimeSpan.FromMinutes(user.QuoteRefreshMinutes));
        return results;
    }

    public async Task<MarketIndicators> IndicatorsAsync(int userId)
    {
        var user = await settings.GetAsync(userId);

        var key = $"indicators:{userId}";
        if (cache.TryGetValue(key, out MarketIndicators? cached) && cached is not null)
            return cached;

        var indicators = await Build(user.MarketDataToken).GetIndicatorsAsync();

        // Rates move monthly at most; refreshing them on the quote interval would spend
        // the rate limit on numbers that cannot have changed.
        cache.Set(key, indicators, TimeSpan.FromHours(6));
        return indicators;
    }

    /// <summary>Drops every cached answer for one user, for the refresh button and after a token change.</summary>
    public void Invalidate(int userId)
    {
        if (cache is MemoryCache concrete)
            concrete.Clear();
        else
            cache.Remove($"indicators:{userId}");
    }
}
