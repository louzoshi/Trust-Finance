using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TrustFinance.E2ETests.Infrastructure;

namespace TrustFinance.E2ETests.Flows;

/// <summary>
/// The journeys a person actually makes, in a real browser against a real server.
///
/// This is the layer that proves the interactive parts work at all: a Blazor Server page
/// only becomes clickable once its SignalR circuit connects, and no HTTP-level test can
/// tell whether that happened.
/// </summary>
[Collection(E2ECollection.Name)]
public class InvestingFlowTests(BrowserFixture fixture)
{
    private async Task<IPage> OpenAsync(string path = "/", int width = 1280)
    {
        var page = await fixture.NewPageAsync(width);
        await page.GotoAsync(fixture.App.BaseUrl + path);

        // Wait for the circuit. window.Blazor exists as soon as the script loads, well
        // before the SignalR connection is up; the layout stamps this attribute on its
        // first interactive render, and until then a click lands on prerendered HTML.
        await page.WaitForFunctionAsync("() => document.documentElement.dataset.interactive === 'true'");
        return page;
    }

    [Fact]
    public async Task A_New_User_Should_Be_Able_To_Record_A_Trade_And_See_The_Position()
    {
        var page = await OpenAsync("/carteira");

        await page.GetByRole(AriaRole.Button, new() { Name = "Nova operação" }).ClickAsync();

        await page.GetByLabel("Ticker").FillAsync("petr4");
        await page.GetByLabel("Quantidade").FillAsync("100");
        await page.GetByLabel("Preço unitário").FillAsync("30");
        await page.GetByLabel("Taxas").FillAsync("4.90");
        await page.GetByRole(AriaRole.Button, new() { Name = "Registrar" }).ClickAsync();

        // The ticker was typed lower-case; the value object upper-cases it end to end.
        await Assertions.Expect(page.Locator("td", new() { HasText = "PETR4" }).First)
            .ToBeVisibleAsync(new() { Timeout = 10_000 });

        // 100 x 30 + 4.90 of fees is an average price of R$ 30,05.
        await Assertions.Expect(page.GetByText("R$ 30,05").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task An_Invalid_Trade_Should_Show_The_Domain_Notification_And_Save_Nothing()
    {
        var page = await OpenAsync("/carteira");

        await page.GetByRole(AriaRole.Button, new() { Name = "Nova operação" }).ClickAsync();
        await page.GetByLabel("Ticker").FillAsync("VALE3");
        await page.GetByLabel("Quantidade").FillAsync("-5");
        await page.GetByLabel("Preço unitário").FillAsync("0");
        await page.GetByRole(AriaRole.Button, new() { Name = "Registrar" }).ClickAsync();

        // Both problems at once — the thing the notification pattern buys over throwing.
        await Assertions.Expect(page.GetByText("A quantidade precisa ser maior que zero"))
            .ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Assertions.Expect(page.GetByText("O preço precisa ser maior que zero")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task The_Market_Screen_Should_List_Assets_And_Filter_By_Class()
    {
        var page = await OpenAsync("/mercado");

        await Assertions.Expect(page.GetByText("PETR4").First).ToBeVisibleAsync(new() { Timeout = 10_000 });

        await page.GetByRole(AriaRole.Button, new() { Name = "FIIs", Exact = true }).ClickAsync();

        await Assertions.Expect(page.GetByText("HGLG11").First).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Assertions.Expect(page.GetByText("PETR4").First).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Following_An_Asset_Should_Put_It_On_The_Watchlist()
    {
        var page = await OpenAsync("/mercado");
        await Assertions.Expect(page.GetByText("ABEV3").First).ToBeVisibleAsync(new() { Timeout = 10_000 });

        var row = page.Locator("tr", new() { HasText = "ABEV3" }).First;
        await row.GetByRole(AriaRole.Button, new() { Name = "Seguir" }).ClickAsync();

        await Assertions.Expect(row.GetByRole(AriaRole.Button, new() { Name = "Seguindo" })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Watchlist") }).ClickAsync();
        await Assertions.Expect(page.GetByText("ABEV3").First).ToBeVisibleAsync();
    }
}
