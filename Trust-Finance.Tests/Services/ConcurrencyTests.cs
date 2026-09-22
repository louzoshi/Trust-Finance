using TrustFinance.App.Services;
using TrustFinance.Domain.Entities;
using TrustFinance.Tests.Fixtures;

namespace TrustFinance.Tests.Services;

/// <summary>
/// Two windows on the same row. Without a version the second save wins by being second and
/// the first correction disappears without a word; these are the tests that say it does not.
/// </summary>
public class ConcurrencyTests : IDisposable
{
    private readonly SqliteDatabase _db = new();
    private readonly TransactionService _service;

    public ConcurrencyTests() => _service = new TransactionService(_db);

    public void Dispose() => _db.Dispose();

    private Transaction Tx(int categoryId, decimal amount = 100m) =>
        new("Mercado", amount, new DateOnly(2026, 9, 10), TransactionType.Expense, categoryId, _db.AdaAccount, _db.Ada);

    [Fact]
    public async Task A_New_Row_Should_Start_At_Version_Zero()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);

        var created = (await _service.CreateAsync(Tx(category.Id))).Value!;

        created.Version.Should().Be(0);
    }

    [Fact]
    public async Task A_Correction_Should_Raise_The_Version()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        var created = (await _service.CreateAsync(Tx(category.Id))).Value!;

        (await _service.UpdateAsync(created.Id, Tx(category.Id, 120m), created.Version)).Success.Should().BeTrue();

        (await _service.GetByIdAsync(created.Id, _db.Ada))!.Version.Should().Be(1);
    }

    [Fact]
    public async Task The_Second_Window_Should_Be_Refused_Rather_Than_Applied()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        var created = (await _service.CreateAsync(Tx(category.Id))).Value!;

        // Both windows opened the same row and read the same version.
        var versionBothSaw = created.Version;

        (await _service.UpdateAsync(created.Id, Tx(category.Id, 120m), versionBothSaw)).Success.Should().BeTrue();
        var second = await _service.UpdateAsync(created.Id, Tx(category.Id, 90m), versionBothSaw);

        second.Failed.Should().BeTrue();
        second.Messages.Should().ContainSingle().Which.Should().Contain("alterado em outra janela");

        // The first correction is still there, which is the whole point.
        (await _service.GetByIdAsync(created.Id, _db.Ada))!.Amount.Should().Be(120m);
    }

    [Fact]
    public async Task Reloading_Should_Let_The_Second_Window_Through()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        var created = (await _service.CreateAsync(Tx(category.Id))).Value!;

        await _service.UpdateAsync(created.Id, Tx(category.Id, 120m), created.Version);

        var reloaded = (await _service.GetByIdAsync(created.Id, _db.Ada))!;
        (await _service.UpdateAsync(created.Id, Tx(category.Id, 90m), reloaded.Version)).Success.Should().BeTrue();

        (await _service.GetByIdAsync(created.Id, _db.Ada))!.Amount.Should().Be(90m);
    }
}
