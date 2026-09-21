using TrustFinance.App.Services;
using TrustFinance.Domain.Entities;
using TrustFinance.Tests.Fixtures;

namespace TrustFinance.Tests.Services;

/// <summary>
/// The property that matters: importing is safe to repeat. The same file twice, the
/// same lines in a later file, and a row the user had already typed by hand must all
/// end with one transaction, not two.
/// </summary>
public class ImportServiceTests : IDisposable
{
    private readonly SqliteDatabase _db = new();
    private readonly ImportService _import;
    private readonly CategoryRuleService _rules;
    private readonly TransactionService _transactions;

    public ImportServiceTests()
    {
        _rules = new CategoryRuleService(_db);
        _import = new ImportService(_db, _rules);
        _transactions = new TransactionService(_db);
    }

    public void Dispose() => _db.Dispose();

    private static string Statement(params string[] lines) => $"""
        OFXHEADER:100
        <OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS>
        <BANKACCTFROM><BANKID>0341<ACCTID>12345-6</BANKACCTFROM>
        <BANKTRANLIST><DTSTART>20260901<DTEND>20260930
        {string.Join('\n', lines)}
        </BANKTRANLIST>
        <LEDGERBAL><BALAMT>1000.00<DTASOF>20260930</LEDGERBAL>
        </STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
        """;

    private static string Line(string fitId, string date, string amount, string memo) =>
        $"<STMTTRN><TRNTYPE>{(amount.StartsWith('-') ? "DEBIT" : "CREDIT")}<DTPOSTED>{date}<TRNAMT>{amount}<FITID>{fitId}<MEMO>{memo}</STMTTRN>";

    private async Task<(int Account, int Category)> SetUpAsync()
    {
        var category = await _db.AddCategoryAsync(_db.Ada, "Mercado");
        return (_db.AdaAccount, category.Id);
    }

    [Fact]
    public async Task A_First_Import_Should_Create_Every_Line()
    {
        var (account, category) = await SetUpAsync();
        var file = Statement(
            Line("a1", "20260903", "-45.90", "MERCADO SILVA"),
            Line("a2", "20260905", "5000.00", "SALARIO"));

        var preview = await _import.PreviewAsync(file, account, _db.Ada);
        preview.Lines.Should().OnlyContain(l => l.Status == ImportStatus.New);
        foreach (var line in preview.Lines) line.CategoryId = category;

        var result = await _import.CommitAsync(preview, account, _db.Ada, learnRules: false);

        result.Success.Should().BeTrue();
        result.Value!.Created.Should().Be(2);

        var rows = await _transactions.GetAllAsync(_db.Ada);
        rows.Should().HaveCount(2);
        rows.Should().Contain(t => t.Description == "MERCADO SILVA" && t.Amount == 45.90m && t.Type == TransactionType.Expense);
        rows.Should().Contain(t => t.Description == "SALARIO" && t.Type == TransactionType.Income);
        rows.Should().OnlyContain(t => t.ExternalId != null && t.AccountId == account);
    }

    [Fact]
    public async Task The_Same_File_Twice_Should_Create_Nothing_The_Second_Time()
    {
        var (account, category) = await SetUpAsync();
        var file = Statement(Line("a1", "20260903", "-45.90", "MERCADO SILVA"));

        var first = await _import.PreviewAsync(file, account, _db.Ada);
        foreach (var l in first.Lines) l.CategoryId = category;
        await _import.CommitAsync(first, account, _db.Ada, learnRules: false);

        var second = await _import.PreviewAsync(file, account, _db.Ada);

        second.Lines.Should().OnlyContain(l => l.Status == ImportStatus.AlreadyImported);
        second.SkippedCount.Should().Be(1);

        var outcome = await _import.CommitAsync(second, account, _db.Ada, learnRules: false);
        outcome.Value!.Created.Should().Be(0);
        (await _transactions.GetAllAsync(_db.Ada)).Should().ContainSingle();
    }

    [Fact]
    public async Task An_Overlapping_File_Should_Only_Add_What_Is_New()
    {
        var (account, category) = await SetUpAsync();

        var september = await _import.PreviewAsync(Statement(Line("a1", "20260903", "-45.90", "MERCADO SILVA")), account, _db.Ada);
        foreach (var l in september.Lines) l.CategoryId = category;
        await _import.CommitAsync(september, account, _db.Ada, learnRules: false);

        var both = await _import.PreviewAsync(
            Statement(Line("a1", "20260903", "-45.90", "MERCADO SILVA"), Line("a2", "20260910", "-80.00", "FARMACIA")),
            account, _db.Ada);

        both.SkippedCount.Should().Be(1);
        both.NewCount.Should().Be(1);
        foreach (var l in both.Lines.Where(l => l.Status == ImportStatus.New)) l.CategoryId = category;

        var outcome = await _import.CommitAsync(both, account, _db.Ada, learnRules: false);
        outcome.Value!.Created.Should().Be(1);
        (await _transactions.GetAllAsync(_db.Ada)).Should().HaveCount(2);
    }

    [Fact]
    public async Task A_Hand_Typed_Row_Should_Be_Reconciled_Rather_Than_Duplicated()
    {
        var (account, category) = await SetUpAsync();

        // Typed on the 4th; the bank posted it on the 3rd. Same amount, one day apart.
        var typed = (await _transactions.CreateAsync(
            new Transaction("Mercado", 45.90m, new DateOnly(2026, 9, 4), TransactionType.Expense, category, account, _db.Ada))).Value!;

        var preview = await _import.PreviewAsync(Statement(Line("a1", "20260903", "-45.90", "MERCADO SILVA")), account, _db.Ada);

        preview.MatchedCount.Should().Be(1);
        preview.Lines[0].Existing!.Id.Should().Be(typed.Id);

        var outcome = await _import.CommitAsync(preview, account, _db.Ada, learnRules: false);

        outcome.Value!.Reconciled.Should().Be(1);
        outcome.Value.Created.Should().Be(0);

        var rows = await _transactions.GetAllAsync(_db.Ada);
        rows.Should().ContainSingle();
        rows[0].Description.Should().Be("Mercado", "the user's own wording is kept");
        rows[0].ExternalId.Should().Be("a1", "but it now knows which bank line it was");
    }

    [Fact]
    public async Task A_Reconciled_Row_Should_Not_Be_Matched_Again_By_A_Later_Line()
    {
        var (account, category) = await SetUpAsync();
        await _transactions.CreateAsync(
            new Transaction("Mercado", 45.90m, new DateOnly(2026, 9, 3), TransactionType.Expense, category, account, _db.Ada));

        // Two identical amounts on the same day: only one can claim the typed row.
        var preview = await _import.PreviewAsync(
            Statement(Line("a1", "20260903", "-45.90", "MERCADO SILVA"), Line("a2", "20260903", "-45.90", "MERCADO SILVA")),
            account, _db.Ada);

        preview.MatchedCount.Should().Be(1);
        preview.NewCount.Should().Be(1);

        foreach (var l in preview.Lines.Where(l => l.Status == ImportStatus.New)) l.CategoryId = category;
        var outcome = await _import.CommitAsync(preview, account, _db.Ada, learnRules: false);

        outcome.Value!.Created.Should().Be(1);
        outcome.Value.Reconciled.Should().Be(1);
        (await _transactions.GetAllAsync(_db.Ada)).Should().HaveCount(2);
    }

    [Fact]
    public async Task A_Row_Too_Far_From_The_Bank_Date_Should_Not_Be_Reconciled()
    {
        var (account, category) = await SetUpAsync();
        await _transactions.CreateAsync(
            new Transaction("Mercado", 45.90m, new DateOnly(2026, 9, 10), TransactionType.Expense, category, account, _db.Ada));

        var preview = await _import.PreviewAsync(Statement(Line("a1", "20260903", "-45.90", "MERCADO SILVA")), account, _db.Ada);

        preview.MatchedCount.Should().Be(0);
        preview.NewCount.Should().Be(1);
    }

    [Fact]
    public async Task The_Same_Bank_Line_On_Another_Account_Should_Still_Be_New()
    {
        var (account, category) = await SetUpAsync();
        var card = await _db.AddCardAsync(_db.Ada);

        var first = await _import.PreviewAsync(Statement(Line("a1", "20260903", "-45.90", "MERCADO SILVA")), account, _db.Ada);
        foreach (var l in first.Lines) l.CategoryId = category;
        await _import.CommitAsync(first, account, _db.Ada, learnRules: false);

        var onCard = await _import.PreviewAsync(Statement(Line("a1", "20260903", "-45.90", "MERCADO SILVA")), card.Id, _db.Ada);

        onCard.NewCount.Should().Be(1, "an id is only unique within the account it came from");
    }

    [Fact]
    public async Task A_Line_Without_A_Category_Should_Stop_The_Whole_Import()
    {
        var (account, category) = await SetUpAsync();
        var preview = await _import.PreviewAsync(
            Statement(Line("a1", "20260903", "-45.90", "MERCADO SILVA"), Line("a2", "20260905", "-10.00", "PADARIA")),
            account, _db.Ada);

        preview.Lines[0].CategoryId = category;

        var result = await _import.CommitAsync(preview, account, _db.Ada, learnRules: false);

        result.Failed.Should().BeTrue();
        result.Messages.Should().ContainSingle().Which.Should().Contain("sem categoria");
        (await _transactions.GetAllAsync(_db.Ada)).Should().BeEmpty("nothing is written until every line can be");
    }

    [Fact]
    public async Task An_Unticked_Line_Should_Be_Left_Out()
    {
        var (account, category) = await SetUpAsync();
        var preview = await _import.PreviewAsync(
            Statement(Line("a1", "20260903", "-45.90", "MERCADO SILVA"), Line("a2", "20260905", "-10.00", "PADARIA")),
            account, _db.Ada);

        foreach (var l in preview.Lines) l.CategoryId = category;
        preview.Lines[1].Include = false;

        var outcome = await _import.CommitAsync(preview, account, _db.Ada, learnRules: false);

        outcome.Value!.Created.Should().Be(1);
        (await _transactions.GetAllAsync(_db.Ada)).Should().ContainSingle(t => t.Description == "MERCADO SILVA");
    }

    [Fact]
    public async Task Learning_Should_Categorize_The_Same_Merchant_Next_Time()
    {
        var (account, category) = await SetUpAsync();

        var first = await _import.PreviewAsync(Statement(Line("a1", "20260903", "-45.90", "PIX ENVIADO 03/09 MERCADO SILVA 0001")), account, _db.Ada);
        first.Lines[0].CategoryId = category;
        var outcome = await _import.CommitAsync(first, account, _db.Ada, learnRules: true);

        outcome.Value!.RulesLearned.Should().Be(1);

        // A different day and a different sequence number: the same merchant all the same.
        var second = await _import.PreviewAsync(Statement(Line("a2", "20260917", "-60.00", "PIX ENVIADO 17/09 MERCADO SILVA 0002")), account, _db.Ada);

        second.Lines[0].CategoryId.Should().Be(category);
    }

    [Fact]
    public async Task An_Import_Should_Not_Reach_Another_Users_Account()
    {
        await SetUpAsync();
        var bobsCategory = await _db.AddCategoryAsync(_db.Bob);

        var preview = await _import.PreviewAsync(Statement(Line("a1", "20260903", "-45.90", "MERCADO SILVA")), _db.BobAccount, _db.Ada);
        preview.Lines[0].CategoryId = bobsCategory.Id;

        var result = await _import.CommitAsync(preview, _db.BobAccount, _db.Ada, learnRules: false);

        result.Failed.Should().BeTrue();
        result.Messages.Should().ContainSingle().Which.Should().Contain("Conta não encontrada");
    }
}
