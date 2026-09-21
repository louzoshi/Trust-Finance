using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using TrustFinance.Domain.Investing;

namespace TrustFinance.App.Services.Market;

/// <summary>
/// The daily CDI, straight from the Banco Central's open-data series (SGS 12). No token,
/// no account: it is the same public endpoint every Brazilian bank's back office reads.
///
/// When the central bank cannot be reached, a flat series at the current annual rate
/// stands in, so the comparison still renders — marked as estimated, never as real.
/// </summary>
public sealed class CdiSeries(IHttpClientFactory httpFactory, IMemoryCache cache, ILogger<CdiSeries> logger)
{
    public const string BaseAddress = "https://api.bcb.gov.br/dados/serie/";
    public const string ClientName = nameof(CdiSeries);

    /// <summary>Daily rates from <paramref name="from"/> to <paramref name="to"/>, and whether they are the real thing.</summary>
    public async Task<(IReadOnlyList<DailyRate> Rates, bool IsLive)> GetAsync(DateOnly from, DateOnly to, decimal fallbackAnnualPercent, CancellationToken ct = default)
    {
        // One fetch per day per range start: the series only grows at the end.
        var key = $"cdi:{from:yyyyMMdd}:{to:yyyyMMdd}";
        if (cache.TryGetValue(key, out (IReadOnlyList<DailyRate>, bool) cached))
            return cached;

        var live = await FetchAsync(from, to, ct);
        var result = live is { Count: > 0 }
            ? (live, true)
            : (Flat(from, to, fallbackAnnualPercent), false);

        cache.Set(key, result, TimeSpan.FromHours(12));
        return result;
    }

    private async Task<List<DailyRate>?> FetchAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        try
        {
            var http = httpFactory.CreateClient(ClientName);
            var path = $"bcdata.sgs.12/dados?formato=json&dataInicial={from:dd/MM/yyyy}&dataFinal={to:dd/MM/yyyy}";
            var rows = await http.GetFromJsonAsync<List<SgsRow>>(path, ct);

            return rows?
                .Select(r => new DailyRate(
                    DateOnly.ParseExact(r.Data!, "dd/MM/yyyy", CultureInfo.InvariantCulture),
                    decimal.Parse(r.Valor!, CultureInfo.InvariantCulture)))
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Banco Central CDI series unavailable; using a flat estimate");
            return null;
        }
    }

    /// <summary>Every weekday at the daily equivalent of <paramref name="annualPercent"/> on a 252-day year.</summary>
    public static List<DailyRate> Flat(DateOnly from, DateOnly to, decimal annualPercent)
    {
        var daily = (decimal)(Math.Pow(1 + (double)annualPercent / 100, 1.0 / 252) - 1) * 100m;
        var rates = new List<DailyRate>();

        for (var d = from; d <= to; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                rates.Add(new DailyRate(d, daily));

        return rates;
    }

    private sealed class SgsRow
    {
        [JsonPropertyName("data")] public string? Data { get; set; }
        [JsonPropertyName("valor")] public string? Valor { get; set; }
    }
}
