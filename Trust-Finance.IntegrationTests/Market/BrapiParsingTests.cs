using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using TrustFinance.App.Services.Market;
using TrustFinance.Domain.Entities;

namespace TrustFinance.IntegrationTests.Market;

/// <summary>
/// The brapi.dev client against canned responses in the exact shape the real API
/// returns. Deterministic and offline: these must pass when B3 is closed, when the
/// network is down and when the free tier has been exhausted.
/// </summary>
public class BrapiParsingTests
{
    // Trimmed from a real https://brapi.dev/api/quote/PETR4 response.
    private const string QuoteJson = """
    {"results":[{"symbol":"PETR4","shortName":"PETR4","longName":"Petroleo Brasileiro SA Pfd",
    "currency":"BRL","regularMarketPrice":48.5,"regularMarketChange":-0.11,
    "regularMarketChangePercent":-0.23,"marketCap":655086146340,
    "regularMarketPreviousClose":48.45}],"requestedAt":"2026-09-21T02:36:48.000Z"}
    """;

    private static BrapiMarketData Client(HttpStatusCode status, string body)
    {
        var http = new HttpClient(new StubHandler(status, body))
        {
            BaseAddress = new Uri(BrapiMarketData.BaseAddress)
        };
        return new BrapiMarketData(http, token: "fake", NullLogger<BrapiMarketData>.Instance);
    }

    [Fact]
    public async Task Should_Map_A_Quote_Response()
    {
        var quotes = await Client(HttpStatusCode.OK, QuoteJson).GetQuotesAsync(["PETR4"]);

        var quote = quotes["PETR4"];
        quote.Name.Should().Be("Petroleo Brasileiro SA Pfd");
        quote.Price.Should().Be(48.5m);
        quote.PreviousClose.Should().Be(48.45m);
        quote.Change.Should().BeApproximately(0.05m, 0.001m);
        quote.IsUp.Should().BeTrue();
    }

    [Fact]
    public async Task Lookup_Should_Be_Case_Insensitive()
    {
        var quotes = await Client(HttpStatusCode.OK, QuoteJson).GetQuotesAsync(["PETR4"]);

        quotes.ContainsKey("petr4").Should().BeTrue("a position stores what the user typed");
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task A_Failed_Call_Should_Return_Nothing_Rather_Than_Throw(HttpStatusCode status)
    {
        // The portfolio screen then prices those holdings at cost and says so. A rate
        // limit must never be able to take a page down.
        var quotes = await Client(status, "").GetQuotesAsync(["PETR4"]);

        quotes.Should().BeEmpty();
    }

    [Fact]
    public async Task Malformed_Json_Should_Be_Swallowed()
    {
        var quotes = await Client(HttpStatusCode.OK, "{ isto nao e json").GetQuotesAsync(["PETR4"]);

        quotes.Should().BeEmpty();
    }

    [Fact]
    public async Task A_Quote_Without_A_Price_Should_Be_Skipped()
    {
        const string noPrice = """{"results":[{"symbol":"XPTO3","longName":"Sem preço"}]}""";

        var quotes = await Client(HttpStatusCode.OK, noPrice).GetQuotesAsync(["XPTO3"]);

        quotes.Should().BeEmpty("a position priced at null is worse than one priced at cost");
    }

    [Fact]
    public async Task Indicators_Should_Fall_Back_When_The_Call_Fails()
    {
        var indicators = await Client(HttpStatusCode.ServiceUnavailable, "").GetIndicatorsAsync();

        indicators.CdiAnnual.Should().BePositive("a benchmark line is better than a blank");
        indicators.IpcaAnnual.Should().BePositive();
    }

    [Theory]
    [InlineData("PETR4")]
    [InlineData("VALE3")]
    [InlineData("ITUB4")]
    public void A_Share_Ticker_Should_Be_Classified_As_A_Stock(string ticker) =>
        BrapiMarketData.Classify(ticker).Should().Be(AssetClass.Stock);

    [Theory]
    [InlineData("HGLG11")]
    [InlineData("BOVA11")]
    public void A_Ticker_Ending_In_11_Should_Be_Guessed_As_A_Fund(string ticker)
    {
        // Documents a known limit rather than pretending to be right: BOVA11 is really
        // an ETF, but the suffix cannot distinguish FII from ETF from unit. The class the
        // user actually owns comes from their trade, so this only labels a browser row.
        BrapiMarketData.Classify(ticker).Should().Be(AssetClass.Fii);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }
}
