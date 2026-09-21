using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using TrustFinance.Domain.Markets;

namespace TrustFinance.App.Services.Market;

/// <summary>
/// The Banco Central's open-data time series (SGS). No token, no account: the same
/// public endpoint every Brazilian bank's back office reads.
///
/// Three series matter here. 12 is the daily CDI, the benchmark almost every
/// post-fixed paper pays a percentage of. 11 is the daily SELIC. 433 is the IPCA,
/// published monthly, which is why an IPCA+ paper accrues in monthly steps.
///
/// When the central bank cannot be reached, a flat series at a supplied annual rate
/// stands in and the caller is told it is an estimate — never passed off as real.
/// </summary>
public sealed class BcbSeries(IHttpClientFactory httpFactory, IMemoryCache cache, ILogger<BcbSeries> logger)
{
    public const string BaseAddress = "https://api.bcb.gov.br/dados/serie/";
    public const string ClientName = nameof(BcbSeries);

    public const int Cdi = 12;
    public const int Selic = 11;
    public const int Ipca = 433;

    /// <summary>Daily CDI over the range, and whether it is the real series.</summary>
    public Task<(IReadOnlyList<DailyRate> Rates, bool IsLive)> GetAsync(
        DateOnly from, DateOnly to, decimal fallbackAnnualPercent, CancellationToken ct = default)
        => GetAsync(Cdi, from, to, fallbackAnnualPercent, ct);

    public async Task<(IReadOnlyList<DailyRate> Rates, bool IsLive)> GetAsync(
        int series, DateOnly from, DateOnly to, decimal fallbackAnnualPercent, CancellationToken ct = default)
    {
        var key = $"bcb:{series}:{from:yyyyMMdd}:{to:yyyyMMdd}";
        if (cache.TryGetValue(key, out (IReadOnlyList<DailyRate>, bool) cached))
            return cached;

        var live = await FetchAsync(series, from, to, ct);
        var result = live is { Count: > 0 }
            ? (live, true)
            : ((IReadOnlyList<DailyRate>)Flat(from, to, fallbackAnnualPercent), false);

        cache.Set(key, result, TimeSpan.FromHours(12));
        return result;
    }

    private async Task<List<DailyRate>?> FetchAsync(int series, DateOnly from, DateOnly to, CancellationToken ct)
    {
        try
        {
            var http = httpFactory.CreateClient(ClientName);
            var path = $"bcdata.sgs.{series}/dados?formato=json&dataInicial={from:dd/MM/yyyy}&dataFinal={to:dd/MM/yyyy}";
            var rows = await http.GetFromJsonAsync<List<SgsRow>>(path, ct);

            return rows?
                .Where(r => r.Data is not null && r.Valor is not null)
                .Select(r => new DailyRate(
                    DateOnly.ParseExact(r.Data!, "dd/MM/yyyy", CultureInfo.InvariantCulture),
                    decimal.Parse(r.Valor!, CultureInfo.InvariantCulture)))
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Banco Central series {Series} unavailable; using a flat estimate", series);
            return null;
        }
    }

    /// <summary>Every business day at the daily equivalent of <paramref name="annualPercent"/> on a 252-day year.</summary>
    public static List<DailyRate> Flat(DateOnly from, DateOnly to, decimal annualPercent)
    {
        var daily = (Rates.DailyFactor(annualPercent) - 1) * 100m;
        var rates = new List<DailyRate>();

        for (var d = from; d <= to; d = d.AddDays(1))
            if (BusinessCalendar.IsBusinessDay(d))
                rates.Add(new DailyRate(d, daily));

        return rates;
    }

    private sealed class SgsRow
    {
        [JsonPropertyName("data")] public string? Data { get; set; }
        [JsonPropertyName("valor")] public string? Valor { get; set; }
    }
}
