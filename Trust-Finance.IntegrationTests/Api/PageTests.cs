using System.Net;
using TrustFinance.IntegrationTests.Infrastructure;

namespace TrustFinance.IntegrationTests.Api;

/// <summary>
/// Every page, through the real pipeline. These catch what unit tests structurally
/// cannot: a missing DI registration, a broken route, a component that throws only once
/// the database is behind it, a migration that does not apply on a fresh file.
/// </summary>
public class PageTests(TrustFinanceApp app) : IClassFixture<TrustFinanceApp>
{
    [Theory]
    [InlineData("/")]
    [InlineData("/lancamentos")]
    [InlineData("/recorrentes")]
    [InlineData("/categorias")]
    [InlineData("/carteira")]
    [InlineData("/mercado")]
    [InlineData("/metas")]
    [InlineData("/configuracoes")]
    public async Task Every_Page_Should_Render(string path)
    {
        var response = await app.CreatePlainClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "{0} must render", path);

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Trust Finance");
        html.Should().NotContain("An unhandled error has occurred");
    }

    [Fact]
    public async Task The_Database_Should_Be_Migrated_On_Startup()
    {
        // A fresh file with no tables would make the dashboard throw; that it renders the
        // empty state proves the migrations ran.
        var html = await app.CreatePlainClient().GetStringAsync("/");

        Html.Text(html).Should().Contain("Vamos começar");
    }

    [Fact]
    public async Task An_Unknown_Address_Should_Return_A_Not_Found_Page()
    {
        var response = await app.CreatePlainClient().GetAsync("/nao-existe-mesmo");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Html.Text(await response.Content.ReadAsStringAsync()).Should().Contain("não encontrada");
    }

    [Fact]
    public async Task Colour_Tokens_Should_Be_Generated_Into_The_Head()
    {
        var html = await app.CreatePlainClient().GetStringAsync("/");

        // Proves ChartPalette really is the single source: the page has no other way
        // to learn these values.
        html.Should().Contain("--series-1:").And.Contain(":root[data-theme=\"dark\"]");
    }

    [Fact]
    public async Task The_Stylesheet_Should_Be_Served()
    {
        var response = await app.CreatePlainClient().GetAsync("/app.css");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("--space-1");
    }
}
