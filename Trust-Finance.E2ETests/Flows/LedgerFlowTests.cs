using Microsoft.Playwright;
using TrustFinance.E2ETests.Infrastructure;

namespace TrustFinance.E2ETests.Flows;

/// <summary>
/// The cash ledger end to end: a recurrence set in the past posts its overdue occurrences
/// the moment it is saved, and the period chips on the transactions screen slice them.
/// </summary>
[Collection(E2ECollection.Name)]
public class LedgerFlowTests(BrowserFixture fixture)
{
    private async Task<IPage> OpenAsync(string path)
    {
        var page = await fixture.NewPageAsync(1280);
        await page.GotoAsync(fixture.App.BaseUrl + path);
        await page.WaitForFunctionAsync("() => document.documentElement.dataset.interactive === 'true'");
        return page;
    }

    [Fact]
    public async Task A_Recurrence_Started_Two_Months_Ago_Should_Post_Three_Rows_And_Filter_By_Period()
    {
        // Every recurrence needs a category, and the screens share one database.
        var categories = await OpenAsync("/categorias");
        await categories.GetByLabel("Nome").FillAsync("Moradia");
        await categories.GetByRole(AriaRole.Button, new() { Name = "Adicionar" }).ClickAsync();
        await Assertions.Expect(categories.Locator("td", new() { HasText = "Moradia" }).First)
            .ToBeVisibleAsync(new() { Timeout = 10_000 });

        // The app runs on the real clock, so the start is anchored to it: the 1st of the
        // month before last is always three occurrences ago — that month, last month, this one.
        var today = DateOnly.FromDateTime(DateTime.Now);
        var start = new DateOnly(today.Year, today.Month, 1).AddMonths(-2);

        var page = await OpenAsync("/recorrentes");
        await page.GetByLabel("Descrição").FillAsync("Aluguel");
        await page.GetByLabel("Valor", new() { Exact = true }).FillAsync("1800");
        await page.GetByLabel("Categoria").SelectOptionAsync(new SelectOptionValue { Label = "Moradia" });
        await page.GetByLabel("Frequência").SelectOptionAsync(new SelectOptionValue { Label = "Mensal" });
        await page.GetByLabel("Primeira data").FillAsync(start.ToString("yyyy-MM-dd"));
        await page.GetByRole(AriaRole.Button, new() { Name = "Adicionar" }).ClickAsync();

        await Assertions.Expect(page.GetByText("3 lançamentos vencidos foram registrados agora."))
            .ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Assertions.Expect(page.Locator("td", new() { HasText = start.AddMonths(3).ToString("dd/MM/yyyy") }).First)
            .ToBeVisibleAsync();

        var ledger = await OpenAsync("/lancamentos");
        var rows = ledger.Locator("tbody tr", new() { HasText = "Aluguel" });

        // "Este mês" is the default: one occurrence, traced back to the recurrence.
        await Assertions.Expect(rows).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await Assertions.Expect(rows.First.GetByText("Recorrente")).ToBeVisibleAsync();

        await ledger.GetByRole(AriaRole.Button, new() { Name = "Tudo", Exact = true }).ClickAsync();
        await Assertions.Expect(rows).ToHaveCountAsync(3, new() { Timeout = 10_000 });
        await Assertions.Expect(ledger.GetByText("= −R$ 5.400,00")).ToBeVisibleAsync();

        await ledger.GetByRole(AriaRole.Button, new() { Name = "Mês passado", Exact = true }).ClickAsync();
        await Assertions.Expect(rows).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await Assertions.Expect(rows.First.GetByText(start.AddMonths(1).ToString("dd/MM/yyyy"))).ToBeVisibleAsync();
    }
}
