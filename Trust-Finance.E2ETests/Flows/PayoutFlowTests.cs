using Microsoft.Playwright;
using TrustFinance.E2ETests.Infrastructure;

namespace TrustFinance.E2ETests.Flows;

/// <summary>A payout entered on screen shows up as yield on cost against the position that earned it.</summary>
[Collection(E2ECollection.Name)]
public class PayoutFlowTests(BrowserFixture fixture)
{
    private async Task<IPage> OpenAsync(string path)
    {
        var page = await fixture.NewPageAsync(1280);
        await page.GotoAsync(fixture.App.BaseUrl + path);
        await page.WaitForFunctionAsync("() => document.documentElement.dataset.interactive === 'true'");
        return page;
    }

    [Fact]
    public async Task A_Fund_Income_Should_Show_As_Yield_On_Cost_On_The_Portfolio()
    {
        var portfolio = await OpenAsync("/carteira");
        await portfolio.GetByRole(AriaRole.Button, new() { Name = "Nova operação" }).ClickAsync();
        await portfolio.GetByLabel("Ticker").FillAsync("HGLG11");
        await portfolio.GetByLabel("Classe").SelectOptionAsync(new SelectOptionValue { Label = "FII" });
        await portfolio.GetByLabel("Quantidade").FillAsync("100");
        await portfolio.GetByLabel("Preço unitário").FillAsync("100");
        await portfolio.GetByLabel("Data").FillAsync(DateTime.Now.AddMonths(-6).ToString("yyyy-MM-dd"));
        await portfolio.GetByRole(AriaRole.Button, new() { Name = "Registrar" }).ClickAsync();
        await Assertions.Expect(portfolio.Locator("td", new() { HasText = "HGLG11" }).First).ToBeVisibleAsync(new() { Timeout = 10_000 });

        var payouts = await OpenAsync("/proventos");
        await payouts.GetByLabel("Ticker").FillAsync("hglg11");
        await payouts.GetByLabel("Tipo").SelectOptionAsync(new SelectOptionValue { Label = "Rendimento" });
        await payouts.GetByLabel("Quantidade").FillAsync("100");
        await payouts.GetByLabel("Valor bruto").FillAsync("110");
        await payouts.GetByRole(AriaRole.Button, new() { Name = "Adicionar" }).ClickAsync();

        // Listed with the ticker upper-cased, and the tile reads 1,10% over the R$ 10.000 cost.
        await Assertions.Expect(payouts.Locator("td", new() { HasText = "HGLG11" }).First).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Assertions.Expect(payouts.GetByText("1,10%")).ToBeVisibleAsync();
    }
}
