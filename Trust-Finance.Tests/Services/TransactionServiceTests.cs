using TrustFinance.App.Services;
using TrustFinance.Domain.Entities;
using TrustFinance.Tests.Fixtures;

namespace TrustFinance.Tests.Services;

public class TransactionServiceTests : IDisposable
{
    private readonly SqliteDatabase _db = new();
    private readonly TransactionService _service;

    private static readonly DateOnly Today = new(2026, 9, 20);

    public TransactionServiceTests() => _service = new TransactionService(_db);

    public void Dispose() => _db.Dispose();

    private static Transaction Tx(int categoryId, int userId,
        decimal amount = 100m, TransactionType type = TransactionType.Expense, string description = "Compra")
        => new(description, amount, Today, type, categoryId, userId);

    [Fact]
    public async Task Create_Should_Add_Transaction_For_User()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);

        var result = await _service.CreateAsync(Tx(category.Id, _db.Ada, 42.50m));

        result.Success.Should().BeTrue();
        result.Value!.Id.Should().BePositive();
        result.Value.Amount.Should().Be(42.50m);
        result.Value.UserId.Should().Be(_db.Ada);
    }

    [Fact]
    public async Task Create_Should_Reject_A_Category_Owned_By_Somebody_Else()
    {
        var bobs = await _db.AddCategoryAsync(_db.Bob);

        var result = await _service.CreateAsync(Tx(bobs.Id, _db.Ada));

        result.Failed.Should().BeTrue();
        result.Messages.Should().ContainSingle().Which.Should().Contain("Categoria não encontrada");
    }

    [Theory]
    [InlineData(0, "Amount")]
    [InlineData(-10, "Amount")]
    public async Task Create_Should_Reject_A_Non_Positive_Amount(decimal amount, string key)
    {
        var category = await _db.AddCategoryAsync(_db.Ada);

        var result = await _service.CreateAsync(Tx(category.Id, _db.Ada, amount));

        result.Failed.Should().BeTrue();
        result.Notifications.Should().Contain(n => n.Key == key);
    }

    [Fact]
    public async Task Create_Should_Reject_A_Description_That_Is_Too_Short()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);

        var result = await _service.CreateAsync(Tx(category.Id, _db.Ada, description: "x"));

        result.Failed.Should().BeTrue();
        result.Notifications.Should().Contain(n => n.Key == "Description");
    }

    [Fact]
    public async Task Create_Should_Persist_The_Type()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);

        await _service.CreateAsync(Tx(category.Id, _db.Ada, 5000m, TransactionType.Income));

        var stored = (await _service.GetAllAsync(_db.Ada)).Single();
        stored.Type.Should().Be(TransactionType.Income);
    }

    [Fact]
    public async Task Amount_Should_Keep_Its_Cents_Through_The_Database()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);

        await _service.CreateAsync(Tx(category.Id, _db.Ada, 1234.56m));

        (await _service.GetAllAsync(_db.Ada)).Single().Amount.Should().Be(1234.56m);
    }

    [Fact]
    public async Task Update_Should_Change_The_Type()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        var created = (await _service.CreateAsync(Tx(category.Id, _db.Ada))).Value!;

        var result = await _service.UpdateAsync(
            created.Id, Tx(category.Id, _db.Ada, 100m, TransactionType.Income));

        result.Success.Should().BeTrue();
        (await _service.GetByIdAsync(created.Id, _db.Ada))!.Type.Should().Be(TransactionType.Income);
    }

    [Fact]
    public async Task Update_Should_Not_Reach_Another_Users_Transaction()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        var bobs = await _db.AddCategoryAsync(_db.Bob);
        var created = (await _service.CreateAsync(Tx(category.Id, _db.Ada))).Value!;

        var result = await _service.UpdateAsync(created.Id, Tx(bobs.Id, _db.Bob, 999m));

        result.Failed.Should().BeTrue();
        (await _service.GetByIdAsync(created.Id, _db.Ada))!.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task Delete_Should_Not_Reach_Another_Users_Transaction()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        var created = (await _service.CreateAsync(Tx(category.Id, _db.Ada))).Value!;

        (await _service.DeleteAsync(created.Id, _db.Bob)).Failed.Should().BeTrue();
        (await _service.GetByIdAsync(created.Id, _db.Ada)).Should().NotBeNull();
    }

    [Fact]
    public async Task GetAll_Should_List_Newest_First()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);

        await _service.CreateAsync(new Transaction("Antiga", 10m, Today.AddDays(-5), TransactionType.Expense, category.Id, _db.Ada));
        await _service.CreateAsync(new Transaction("Nova", 20m, Today, TransactionType.Expense, category.Id, _db.Ada));

        var all = await _service.GetAllAsync(_db.Ada);

        all.Select(t => t.Description).Should().Equal("Nova", "Antiga");
    }

    [Fact]
    public async Task Deleting_A_Category_Should_Delete_Its_Transactions()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        await _service.CreateAsync(Tx(category.Id, _db.Ada));

        var categories = new CategoryService(_db);
        (await categories.DeleteAsync(category.Id, _db.Ada)).Success.Should().BeTrue();

        (await _service.GetAllAsync(_db.Ada)).Should().BeEmpty("the cascade is enforced by the database");
    }
}
