using System.Net;
using TrustFinance.IntegrationTests.Infrastructure;

namespace TrustFinance.IntegrationTests.Api;

/// <summary>
/// The bypass flag decides whether the app asks for a password. Both sides are tested,
/// because a flag that silently stopped working in the "off" position would be a hole.
/// </summary>
public class AuthenticationTests
{
    [Fact]
    public async Task With_Bypass_On_The_App_Should_Open_Without_Signing_In()
    {
        await using var app = new TrustFinanceApp { BypassAuth = true };

        var response = await app.CreatePlainClient().GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Html.Text(await response.Content.ReadAsStringAsync()).Should().Contain("sem login");
    }

    [Fact]
    public async Task With_Bypass_Off_An_Anonymous_Request_Should_Be_Sent_To_Login()
    {
        await using var app = new TrustFinanceApp { BypassAuth = false };

        var response = await app.CreatePlainClient().GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("/login");
    }

    [Fact]
    public async Task Registering_Should_Set_The_Auth_Cookie_And_Land_On_The_Dashboard()
    {
        await using var app = new TrustFinanceApp { BypassAuth = false };
        var client = app.CreatePlainClient();

        var form = await client.GetStringAsync("/registrar");
        var token = Html.AntiforgeryToken(form);
        token.Should().NotBeNull("the register form must be antiforgery protected");

        var response = await client.PostAsync("/registrar", new FormUrlEncodedContent(
        [
            new("_handler", "register"),
            new("__RequestVerificationToken", token!),
            new("Form.Name", "Ada Lovelace"),
            new("Form.Email", "ada@example.com"),
            new("Form.Password", "senhasegura1")
        ]));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.AbsolutePath.Should().Be("/");
        response.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith("tf.auth"));
    }

    [Fact]
    public async Task Registering_With_A_Bad_Email_Should_Show_The_Domain_Notification()
    {
        await using var app = new TrustFinanceApp { BypassAuth = false };
        var client = app.CreatePlainClient();

        var token = Html.AntiforgeryToken(await client.GetStringAsync("/registrar"))!;

        var response = await client.PostAsync("/registrar", new FormUrlEncodedContent(
        [
            new("_handler", "register"),
            new("__RequestVerificationToken", token),
            new("Form.Name", "Ada Lovelace"),
            new("Form.Email", "nao-e-email"),
            new("Form.Password", "senhasegura1")
        ]));

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the form is redisplayed, not redirected");
        Html.Text(await response.Content.ReadAsStringAsync()).Should().Contain("E-mail inválido");
    }

    [Fact]
    public async Task A_Form_Post_Without_An_Antiforgery_Token_Should_Be_Rejected()
    {
        await using var app = new TrustFinanceApp { BypassAuth = false };

        var response = await app.CreatePlainClient().PostAsync("/registrar", new FormUrlEncodedContent(
        [
            new("_handler", "register"),
            new("Form.Name", "Mallory"),
            new("Form.Email", "mallory@example.com"),
            new("Form.Password", "senhasegura1")
        ]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
