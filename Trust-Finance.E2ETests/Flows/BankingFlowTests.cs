using Microsoft.Playwright;
using TrustFinance.E2ETests.Infrastructure;

namespace TrustFinance.E2ETests.Flows;

/// <summary>
/// An OFX statement dropped on the import screen, twice. The second time the app must
/// recognise every line and create nothing — the property the whole import rests on.
/// </summary>
[Collection(E2ECollection.Name)]
public class BankingFlowTests(BrowserFixture fixture)
{
    private const string Ofx = """
        OFXHEADER:100
        <OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS>
        <BANKACCTFROM><BANKID>0341<ACCTID>12345-6</BANKACCTFROM>
        <BANKTRANLIST><DTSTART>20260901<DTEND>20260930
        <STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20260903<TRNAMT>-45.90<FITID>e2e-1<MEMO>MERCADO SILVA</STMTTRN>
        <STMTTRN><TRNTYPE>CREDIT<DTPOSTED>20260905<TRNAMT>5000.00<FITID>e2e-2<MEMO>SALARIO</STMTTRN>
        </BANKTRANLIST>
        <LEDGERBAL><BALAMT>4954.10<DTASOF>20260930</LEDGERBAL>
        </STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
        """;

    private async Task<IPage> OpenAsync(string path)
    {
        var page = await fixture.NewPageAsync(1280);
        await page.GotoAsync(fixture.App.BaseUrl + path);
        await page.WaitForFunctionAsync("() => document.documentElement.dataset.interactive === 'true'");
        return page;
    }

    private static async Task UploadAsync(IPage page)
    {
        await page.Locator("input[type=file]").SetInputFilesAsync(new FilePayload
        {
            Name = "extrato.ofx",
            MimeType = "text/plain",
            Buffer = System.Text.Encoding.UTF8.GetBytes(Ofx)
        });
    }

    [Fact]
    public async Task Importing_A_Statement_Twice_Should_Create_Its_Lines_Only_Once()
    {
        // A category of its own, so the picks below cannot land on another flow's.
        var categories = await OpenAsync("/categorias");
        await categories.GetByLabel("Nome").FillAsync("Conciliado");
        await categories.GetByRole(AriaRole.Button, new() { Name = "Adicionar" }).ClickAsync();
        await Assertions.Expect(categories.Locator("td", new() { HasText = "Conciliado" }).First)
            .ToBeVisibleAsync(new() { Timeout = 10_000 });

        var import = await OpenAsync("/importar");
        await UploadAsync(import);

        await Assertions.Expect(import.Locator("tbody tr")).ToHaveCountAsync(2, new() { Timeout = 15_000 });
        await Assertions.Expect(import.GetByText("MERCADO SILVA")).ToBeVisibleAsync();

        foreach (var select in await import.Locator("tbody select").AllAsync())
            await select.SelectOptionAsync(new SelectOptionValue { Label = "Conciliado" });

        await import.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("^Importar") }).ClickAsync();
        await Assertions.Expect(import.GetByText("Importação concluída")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Assertions.Expect(import.GetByText("2 lançamentos criados")).ToBeVisibleAsync();

        // The same file again: both lines are recognised and nothing is created.
        var again = await OpenAsync("/importar");
        await UploadAsync(again);

        await Assertions.Expect(again.GetByText("já importado").First).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Assertions.Expect(again.Locator("text=já importado")).ToHaveCountAsync(2);

        await again.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("^Importar") }).ClickAsync();
        await Assertions.Expect(again.GetByText("0 lançamentos criados")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        // And the ledger holds one row per imported line, not two. Scoped to these
        // descriptions: the suite shares one database with the other flows.
        var ledger = await OpenAsync("/lancamentos");
        await ledger.GetByRole(AriaRole.Button, new() { Name = "Tudo", Exact = true }).ClickAsync();
        await Assertions.Expect(ledger.Locator("tbody tr", new() { HasText = "MERCADO SILVA" })).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await Assertions.Expect(ledger.Locator("tbody tr", new() { HasText = "SALARIO" })).ToHaveCountAsync(1);
        await Assertions.Expect(ledger.Locator("tbody tr", new() { HasText = "MERCADO SILVA" }).GetByText("Extrato")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Pasting_A_Pix_Code_Should_Fill_The_Form()
    {
        var page = await OpenAsync("/lancamentos");

        await page.GetByRole(AriaRole.Button, new() { Name = "Colar boleto ou Pix" }).ClickAsync();
        await page.Locator(".paste-input").FillAsync(
            "00020126570014br.gov.bcb.pix0114+55119999999990217Padaria do Bairro5204000053039865406123.455802BR5913FULANO DE TAL6009SAO PAULO62090505TX1236304EAF0");
        await page.GetByRole(AriaRole.Button, new() { Name = "Ler" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Pix para FULANO DE TAL")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Assertions.Expect(page.GetByLabel("Valor", new() { Exact = true })).ToHaveValueAsync("123.45");
    }
}
