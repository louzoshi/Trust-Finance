using TrustFinance.App.Services;
using TrustFinance.Domain.Entities;
using TrustFinance.Tests.Fixtures;

namespace TrustFinance.Tests.Services;

/// <summary>
/// The trail is written by the database context itself, not by the services, so these tests
/// go through the ordinary service calls: if a line only appears when somebody remembered to
/// write it, the trail is worth nothing on the day it matters.
/// </summary>
public class AuditTrailTests : IDisposable
{
    private readonly SqliteDatabase _db = new();
    private readonly TransactionService _transactions;
    private readonly AuditService _audit;

    public AuditTrailTests()
    {
        _transactions = new TransactionService(_db);
        _audit = new AuditService(_db);
    }

    public void Dispose() => _db.Dispose();

    private Transaction Tx(int categoryId, decimal amount = 100m, int? userId = null) =>
        new("Mercado", amount, new DateOnly(2026, 9, 10), TransactionType.Expense, categoryId,
            _db.AccountFor(userId ?? _db.Ada), userId ?? _db.Ada);

    [Fact]
    public async Task Creating_A_Row_Should_Leave_A_Line()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        var created = (await _transactions.CreateAsync(Tx(category.Id))).Value!;

        // Filtered: the fixture's own account and the category above are audited too, which
        // is itself the point — everything that touches a row leaves a line.
        var entry = (await _audit.RecentAsync(_db.Ada, nameof(Transaction))).Should().ContainSingle().Subject;

        entry.Entity.Should().Be(nameof(Transaction));
        entry.EntityId.Should().Be(created.Id);
        entry.Action.Should().Be(AuditAction.Created);
    }

    [Fact]
    public async Task A_Correction_Should_Record_What_Moved_And_What_Did_Not()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        var created = (await _transactions.CreateAsync(Tx(category.Id))).Value!;

        await _transactions.UpdateAsync(created.Id, Tx(category.Id, 120m), created.Version);

        var entry = (await _audit.RecentAsync(_db.Ada, nameof(Transaction))).First();

        entry.Action.Should().Be(AuditAction.Updated);
        entry.Changes.Should().Contain(nameof(Transaction.Amount)).And.Contain("120");

        // The version moves on every write by definition, so it is left out of the diff.
        entry.Changes.Should().NotContain(nameof(IVersioned.Version));

        // Nothing else was touched, so nothing else is reported.
        entry.Changes.Should().NotContain(nameof(Transaction.Date));
    }

    [Fact]
    public async Task A_Correction_That_Changed_Nothing_Should_Not_Be_Recorded()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        var created = (await _transactions.CreateAsync(Tx(category.Id))).Value!;

        await _transactions.UpdateAsync(created.Id, Tx(category.Id), created.Version);

        (await _audit.RecentAsync(_db.Ada, nameof(Transaction))).Should().ContainSingle()
            .Which.Action.Should().Be(AuditAction.Created);
    }

    [Fact]
    public async Task Deleting_A_Row_Should_Leave_The_Line_Behind()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        var created = (await _transactions.CreateAsync(Tx(category.Id))).Value!;

        await _transactions.DeleteAsync(created.Id, _db.Ada);

        var entry = (await _audit.RecentAsync(_db.Ada, nameof(Transaction))).First();

        entry.Action.Should().Be(AuditAction.Deleted);
        entry.EntityId.Should().Be(created.Id);

        // The row is gone and the trail is not: that is the case the trail exists for.
        (await _transactions.GetByIdAsync(created.Id, _db.Ada)).Should().BeNull();
    }

    [Fact]
    public async Task The_Trail_Should_Be_Per_User()
    {
        var adas = await _db.AddCategoryAsync(_db.Ada);
        var bobs = await _db.AddCategoryAsync(_db.Bob);

        await _transactions.CreateAsync(Tx(adas.Id));
        await _transactions.CreateAsync(Tx(bobs.Id, 50m, _db.Bob));

        (await _audit.RecentAsync(_db.Ada, nameof(Transaction))).Should().ContainSingle()
            .Which.UserId.Should().Be(_db.Ada);
        (await _audit.RecentAsync(_db.Bob, nameof(Transaction))).Should().ContainSingle()
            .Which.UserId.Should().Be(_db.Bob);
    }

    [Fact]
    public async Task The_Filter_Should_Offer_Only_What_Was_Touched()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        await _transactions.CreateAsync(Tx(category.Id));

        var entities = await _audit.EntitiesAsync(_db.Ada);

        entities.Should().Contain(nameof(Transaction)).And.Contain(nameof(Category));
        entities.Should().NotContain(nameof(Payout));
    }
}
