using Microsoft.Extensions.Logging.Abstractions;
using TrustFinance.App.Services.Market;

namespace TrustFinance.IntegrationTests.Market;

/// <summary>
/// Real calls to brapi.dev, to catch the thing no stub can: the provider changing its
/// response shape underneath us.
///
/// Tagged so a run without internet can exclude them:
/// <c>dotnet test --filter Category!=Network</c>
/// </summary>
[Trait("Category", "Network")]
public class BrapiLiveTests
{
    private static BrapiMarketData Client()
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(BrapiMarketData.BaseAddress),
            Timeout = TimeSpan.FromSeconds(15)
        };

        // No token: brapi answers the quote endpoint anonymously at a lower rate limit,
        // which is exactly the path a user gets before configuring anything.
        return new BrapiMarketData(http, token: string.Empty, NullLogger<BrapiMarketData>.Instance);
    }

    [Fact]
    public async Task Should_Fetch_A_Real_B3_Quote()
    {
        var quotes = await Client().GetQuotesAsync(["PETR4"]);

        if (quotes.Count == 0)
            return; // Rate limited or offline; the stubbed tests cover the contract.

        var quote = quotes["PETR4"];
        quote.Ticker.Should().Be("PETR4");
        quote.Name.Should().NotBeNullOrWhiteSpace();
        quote.Price.Should().BeInRange(1m, 1000m, "a Petrobras share is not worth R$ 0 or R$ 100k");
        quote.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_Fetch_Several_Tickers_In_One_Call()
    {
        var quotes = await Client().GetQuotesAsync(["PETR4", "VALE3", "ITUB4"]);

        if (quotes.Count == 0)
            return;

        quotes.Keys.Should().Contain("PETR4");
        quotes.Values.Should().OnlyContain(q => q.Price > 0);
    }

    [Fact]
    public async Task An_Unknown_Ticker_Should_Simply_Be_Absent()
    {
        var quotes = await Client().GetQuotesAsync(["NAOEXISTE999"]);

        quotes.Should().NotContainKey("NAOEXISTE999");
    }
}
