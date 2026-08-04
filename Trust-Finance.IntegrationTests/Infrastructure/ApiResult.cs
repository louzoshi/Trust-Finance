using System.Text.Json;

namespace Trust_Finance.IntegrationTests.Infrastructure;

/// <summary>
/// Test-side mirror of the API's <c>ResultViewModel&lt;T&gt;</c> envelope. The production
/// type has private setters and several constructors, so it cannot round-trip through
/// System.Text.Json; deserializing into this record keeps the API surface untouched.
/// </summary>
public sealed record ApiResult<T>(T? Data, List<string>? Errors)
{
    public IReadOnlyList<string> ErrorMessages => Errors ?? new List<string>();
}

public static class HttpResponseMessageExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<ApiResult<T>> ReadResultAsync<T>(this HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<ApiResult<T>>(payload, JsonOptions)
            ?? throw new InvalidOperationException($"Unparsable API response: {payload}");
    }

    public static async Task<T> ReadDataAsync<T>(this HttpResponseMessage response)
    {
        var result = await response.ReadResultAsync<T>();

        return result.Data
            ?? throw new InvalidOperationException(
                $"Response carried no data. Errors: {string.Join("; ", result.ErrorMessages)}");
    }
}
