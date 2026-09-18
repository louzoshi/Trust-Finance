using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using TF.Models;
using Trust_Finance.IntegrationTests.Infrastructure;

namespace Trust_Finance.IntegrationTests.Api;

/// <summary>
/// Covers the parts of the auth setup that only exist once the whole pipeline is running:
/// the bearer middleware, the signing key, and the role policies.
/// </summary>
public class AuthorizationTests : IntegrationTestBase
{
    public AuthorizationTests(TrustFinanceApiFactory factory) : base(factory) { }

    [Theory]
    [InlineData("GET", "/api/categories")]
    [InlineData("POST", "/api/categories")]
    [InlineData("GET", "/api/transactions")]
    [InlineData("POST", "/api/transactions")]
    public async Task Protected_endpoints_reject_anonymous_callers(string method, string route)
    {
        var response = await Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), route));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_public_root_endpoint_stays_anonymous()
    {
        var response = await Client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_rejected()
    {
        var forged = TestTokens.Create("a-completely-different-signing-key-value", userId: 1);

        var response = await SendWithTokenAsync(forged);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_expired_token_is_rejected()
    {
        var expired = TestTokens.Create(
            TrustFinanceApiFactory.JwtSigningKey,
            userId: 1,
            lifetime: TimeSpan.FromHours(-1));

        var response = await SendWithTokenAsync(expired);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_tampered_token_payload_is_rejected()
    {
        var user = await SignUpAsync("ada@trustfinance.dev");
        var segments = user.Token.Split('.');

        // Swap in a different payload while keeping the original signature.
        segments[1] = TestTokens.Create(TrustFinanceApiFactory.JwtSigningKey, userId: 999).Split('.')[1];

        var response = await SendWithTokenAsync(string.Join('.', segments));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> SendWithTokenAsync(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await client.GetAsync("/api/categories");
    }
}
