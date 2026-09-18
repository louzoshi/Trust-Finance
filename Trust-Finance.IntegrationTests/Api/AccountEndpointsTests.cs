using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TF.Models;
using Trust_Finance.IntegrationTests.Infrastructure;

namespace Trust_Finance.IntegrationTests.Api;

public class AccountEndpointsTests : IntegrationTestBase
{
    public AccountEndpointsTests(TrustFinanceApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Register_persists_the_user_with_a_hashed_password()
    {
        var response = await RegisterAsync("ada@trustfinance.dev", "Str0ngPass1");

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var context = CreateDbContext();
        var stored = await context.Users.SingleAsync(u => u.Email == "ada@trustfinance.dev");

        stored.PasswordHash.Should().NotBeEmpty().And.NotBe("Str0ngPass1");
    }

    [Fact]
    public async Task Register_never_returns_the_password_hash()
    {
        var response = await RegisterAsync("ada@trustfinance.dev");

        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContainEquivalentOf("passwordHash");
        body.Should().NotContainEquivalentOf("Str0ngPass1");
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_email_with_400()
    {
        await RegisterAsync("ada@trustfinance.dev");

        var second = await RegisterAsync("ada@trustfinance.dev");

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await second.ReadResultAsync<User>()).ErrorMessages
            .Should().Contain(e => e.Contains("already registered"));

        await using var context = CreateDbContext();
        (await context.Users.CountAsync(u => u.Email == "ada@trustfinance.dev")).Should().Be(1);
    }

    [Theory]
    [InlineData("not-an-email", "Str0ngPass1", "Invalid email")]
    [InlineData("ada@trustfinance.dev", "123", "Password must be between 6 and 20 characters")]
    public async Task Register_rejects_invalid_payloads_with_400(string email, string password, string expectedError)
    {
        var response = await RegisterAsync(email, password);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadResultAsync<User>()).ErrorMessages.Should().Contain(expectedError);

        await using var context = CreateDbContext();
        (await context.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Login_returns_a_jwt_carrying_the_user_id()
    {
        await RegisterAsync("ada@trustfinance.dev", "Str0ngPass1");

        var response = await Client.PostAsJsonAsync(
            "/api/account/login",
            new { email = "ada@trustfinance.dev", password = "Str0ngPass1" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.ReadResultAsync<string>();
        result.ErrorMessages.Should().BeEmpty("the token belongs in data, not in errors");

        result.Data.Should().NotBeNullOrWhiteSpace();

        await using var context = CreateDbContext();
        var user = await context.Users.SingleAsync();

        // TokenService writes ClaimTypes.*, which the JWT handler emits under its short names.
        var claims = TestTokens.Read(result.Data!).Claims.ToList();
        claims.Should().Contain(c => c.Type == "nameid" && c.Value == user.Id.ToString());
        claims.Should().NotContain(c => c.Type == "role",
            "there are no roles left to carry, so the token should not claim one");
    }

    [Fact]
    public async Task Login_with_a_wrong_password_returns_401()
    {
        await RegisterAsync("ada@trustfinance.dev", "Str0ngPass1");

        var response = await Client.PostAsJsonAsync(
            "/api/account/login",
            new { email = "ada@trustfinance.dev", password = "WrongPass1" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.ReadResultAsync<string>()).ErrorMessages
            .Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task Login_with_an_unknown_email_returns_the_same_401_as_a_wrong_password()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/account/login",
            new { email = "nobody@trustfinance.dev", password = "Str0ngPass1" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.ReadResultAsync<string>()).ErrorMessages
            .Should().Contain("Invalid username or password",
                "a distinct message would let an attacker enumerate registered emails");
    }

    [Fact]
    public async Task Login_trims_surrounding_whitespace_on_the_email()
    {
        await RegisterAsync("ada@trustfinance.dev", "Str0ngPass1");

        var response = await Client.PostAsJsonAsync(
            "/api/account/login",
            new { email = "  ada@trustfinance.dev  ", password = "Str0ngPass1" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
