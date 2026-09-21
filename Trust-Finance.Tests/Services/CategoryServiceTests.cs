using TrustFinance.App.Services;
using TrustFinance.Tests.Fixtures;

namespace TrustFinance.Tests.Services;

public class CategoryServiceTests : IDisposable
{
    private readonly SqliteDatabase _db = new();
    private readonly CategoryService _service;

    public CategoryServiceTests() => _service = new CategoryService(_db);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Create_Should_Derive_The_Slug_From_The_Name()
    {
        var result = await _service.CreateAsync("Alimentação ", _db.Ada);

        result.Success.Should().BeTrue();
        result.Value!.Name.Should().Be("Alimentação");
        result.Value.Slug.Should().Be("alimentacao");
        result.Value.UserId.Should().Be(_db.Ada);
    }

    [Fact]
    public async Task Create_Should_Reject_A_Name_That_Is_Too_Short()
    {
        var result = await _service.CreateAsync("A", _db.Ada);

        result.Failed.Should().BeTrue();
        result.Notifications.Should().ContainSingle(n => n.Key == "Name");
    }

    [Fact]
    public async Task Create_Should_Fail_When_The_Name_Collides_For_The_Same_User()
    {
        await _service.CreateAsync("Alimentação", _db.Ada);

        var result = await _service.CreateAsync("alimentacao", _db.Ada);

        result.Failed.Should().BeTrue();
        result.Messages.Should().ContainSingle().Which.Should().Contain("Já existe");
    }

    [Fact]
    public async Task Create_Should_Allow_The_Same_Name_For_Different_Users()
    {
        await _service.CreateAsync("Alimentação", _db.Ada);

        var result = await _service.CreateAsync("Alimentação", _db.Bob);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetAll_Should_Return_Only_The_Callers_Categories()
    {
        await _service.CreateAsync("Mercado", _db.Ada);
        await _service.CreateAsync("Lazer", _db.Bob);

        var categories = await _service.GetAllAsync(_db.Ada);

        categories.Should().ContainSingle().Which.Name.Should().Be("Mercado");
    }

    [Fact]
    public async Task GetById_Should_Not_Reach_Another_Users_Category()
    {
        var created = (await _service.CreateAsync("Mercado", _db.Ada)).Value!;

        (await _service.GetByIdAsync(created.Id, _db.Bob)).Should().BeNull();
    }

    [Fact]
    public async Task Update_Should_Not_Reach_Another_Users_Category()
    {
        var created = (await _service.CreateAsync("Mercado", _db.Ada)).Value!;

        var result = await _service.UpdateAsync(created.Id, "Outro", _db.Bob);

        result.Failed.Should().BeTrue();
        result.Messages.Should().ContainSingle().Which.Should().Contain("não encontrada");
    }

    [Fact]
    public async Task Delete_Should_Not_Reach_Another_Users_Category()
    {
        var created = (await _service.CreateAsync("Mercado", _db.Ada)).Value!;

        (await _service.DeleteAsync(created.Id, _db.Bob)).Failed.Should().BeTrue();
        (await _service.GetByIdAsync(created.Id, _db.Ada)).Should().NotBeNull();
    }

    [Fact]
    public async Task Update_Should_Allow_Keeping_The_Same_Name()
    {
        var created = (await _service.CreateAsync("Mercado", _db.Ada)).Value!;

        (await _service.UpdateAsync(created.Id, "Mercado", _db.Ada)).Success.Should().BeTrue();
    }

    [Fact]
    public async Task Update_Should_Fail_When_Renaming_Onto_Another_Category()
    {
        await _service.CreateAsync("Mercado", _db.Ada);
        var lazer = (await _service.CreateAsync("Lazer", _db.Ada)).Value!;

        var result = await _service.UpdateAsync(lazer.Id, "Mercado", _db.Ada);

        result.Failed.Should().BeTrue();
        result.Messages.Should().ContainSingle().Which.Should().Contain("Já existe");
    }
}
