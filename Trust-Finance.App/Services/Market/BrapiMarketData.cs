using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Investing;

namespace TrustFinance.App.Services.Market;

/// <summary>
/// Live B3 data from brapi.dev.
///
/// Every call degrades rather than throws: a rate limit, an expired token or a dropped
/// connection returns what could be fetched, and the caller shows the rest at cost. A
/// portfolio screen that refuses to render because a third party is down is worse than
/// one that renders honestly with gaps.
/// </summary>
public sealed class BrapiMarketData(HttpClient http, string token, ILogger<BrapiMarketData> logger)
    : IMarketDataProvider
{
    public const string BaseAddress = "https://brapi.dev/api/";

    public string Name => "brapi.dev";
    public bool IsLive => true;

    public async Task<IReadOnlyDictionary<string, Quote>> GetQuotesAsync(
        IEnumerable<string> tickers, CancellationToken ct = default)
    {
        var quotes = new Dictionary<string, Quote>(StringComparer.OrdinalIgnoreCase);

        var list = tickers.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (list.Count == 0)
            return quotes;

        // The endpoint takes a comma-separated batch; chunked so a long watchlist does not
        // build a URL the server rejects.
        foreach (var batch in list.Chunk(20))
        {
            var response = await GetAsync<QuoteResponse>(
                $"quote/{string.Join(',', batch)}?{Auth()}", ct);

            foreach (var r in response?.Results ?? [])
            {
                if (r.Symbol is null || r.RegularMarketPrice is not { } price)
                    continue;

                quotes[r.Symbol] = new Quote(
                    r.Symbol,
                    r.LongName ?? r.ShortName ?? r.Symbol,
                    Classify(r.Symbol),
                    price,
                    r.RegularMarketPreviousClose,
                    null,
                    r.MarketCap,
                    DateTime.UtcNow);
            }
        }

        return quotes;
    }

    public async Task<IReadOnlyList<Quote>> BrowseAsync(
        AssetClass? assetClass, string? search, int limit = 40, CancellationToken ct = default)
    {
        var query = $"quote/list?{Auth()}&limit={limit}";

        if (TypeFor(assetClass) is { } type)
            query += $"&type={type}";

        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search.Trim())}";

        var response = await GetAsync<ListResponse>(query, ct);

        return
        [
            .. (response?.Stocks ?? [])
                .Where(s => s.Stock is not null && s.Close is not null)
                .Select(s => new Quote(
                    s.Stock!,
                    s.Name ?? s.Stock!,
                    assetClass ?? Classify(s.Stock!),
                    s.Close!.Value,
                    // The list endpoint gives a percentage move rather than a close, so the
                    // close is reconstructed from it to keep Quote's contract honest.
                    s.Change is { } ch and not 0 ? s.Close!.Value / (1 + ch / 100m) : null,
                    null,
                    s.MarketCap,
                    DateTime.UtcNow))
        ];
    }

    public async Task<MarketIndicators> GetIndicatorsAsync(CancellationToken ct = default)
    {
        var selic = await GetAsync<PrimeRateResponse>($"v2/prime-rate?country=brazil&{Auth()}", ct);
        var inflation = await GetAsync<InflationResponse>($"v2/inflation?country=brazil&{Auth()}", ct);

        var fallback = SampleMarketData.Fallback(DateTime.UtcNow);

        var selicRate = Parse(selic?.PrimeRate?.FirstOrDefault()?.Value) ?? fallback.SelicAnnual;
        var ipcaRate = Parse(inflation?.Inflation?.FirstOrDefault()?.Value) ?? fallback.IpcaAnnual;

        // brapi publishes SELIC, not CDI. CDI tracks a tenth of a point under it, which is
        // near enough for a benchmark line and better than showing nothing.
        return new MarketIndicators(
            CdiAnnual: Math.Round(selicRate - 0.10m, 2),
            SelicAnnual: selicRate,
            IpcaAnnual: ipcaRate,
            UpdatedAt: DateTime.UtcNow);
    }

    private string Auth() => string.IsNullOrWhiteSpace(token) ? string.Empty : $"token={Uri.EscapeDataString(token)}";

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct) where T : class
    {
        try
        {
            using var response = await http.GetAsync(path, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("brapi.dev returned {Status} for {Path}", (int)response.StatusCode, path.Split('?')[0]);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException
                                      or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "brapi.dev request failed for {Path}", path.Split('?')[0]);
            return null;
        }
    }

    private static decimal? Parse(string? value) =>
        decimal.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;

    private static string? TypeFor(AssetClass? assetClass) => assetClass switch
    {
        AssetClass.Stock => "stock",
        AssetClass.Fii => "fund",
        AssetClass.Bdr => "bdr",
        _ => null
    };

    /// <summary>
    /// Guesses the class from the ticker's shape: a trailing 3/4/5/6 is an ordinary or
    /// preferred share, and 11 is a fund.
    /// </summary>
    /// <remarks>
    /// A suffix of 11 is genuinely ambiguous on B3 — HGLG11 is a FII, BOVA11 an ETF and
    /// SANB11 a unit — and no amount of string parsing can separate them. FII is the
    /// most common of the three, so it is the guess. The class a user actually holds
    /// comes from their own trade, which is stored; this is only used to label a row in
    /// the market browser, where being wrong is cosmetic.
    /// </remarks>
    internal static AssetClass Classify(string ticker)
    {
        var t = ticker.ToUpperInvariant();

        if (t.EndsWith("11") || t.EndsWith("11B")) return AssetClass.Fii;
        if (t.Length >= 2 && t[^2] == '3' && char.IsDigit(t[^1])) return AssetClass.Bdr;
        if (t.Length > 0 && t[^1] is '3' or '4' or '5' or '6') return AssetClass.Stock;

        return AssetClass.Stock;
    }

    private sealed class QuoteResponse
    {
        [JsonPropertyName("results")] public List<QuoteResult>? Results { get; set; }
    }

    private sealed class QuoteResult
    {
        [JsonPropertyName("symbol")] public string? Symbol { get; set; }
        [JsonPropertyName("shortName")] public string? ShortName { get; set; }
        [JsonPropertyName("longName")] public string? LongName { get; set; }
        [JsonPropertyName("regularMarketPrice")] public decimal? RegularMarketPrice { get; set; }
        [JsonPropertyName("regularMarketPreviousClose")] public decimal? RegularMarketPreviousClose { get; set; }
        [JsonPropertyName("marketCap")] public decimal? MarketCap { get; set; }
    }

    private sealed class ListResponse
    {
        [JsonPropertyName("stocks")] public List<ListItem>? Stocks { get; set; }
    }

    private sealed class ListItem
    {
        [JsonPropertyName("stock")] public string? Stock { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("close")] public decimal? Close { get; set; }
        [JsonPropertyName("change")] public decimal? Change { get; set; }
        [JsonPropertyName("market_cap")] public decimal? MarketCap { get; set; }
    }

    private sealed class PrimeRateResponse
    {
        [JsonPropertyName("prime-rate")] public List<RateEntry>? PrimeRate { get; set; }
    }

    private sealed class InflationResponse
    {
        [JsonPropertyName("inflation")] public List<RateEntry>? Inflation { get; set; }
    }

    private sealed class RateEntry
    {
        [JsonPropertyName("value")] public string? Value { get; set; }
    }
}
