using FluentAssertions;
using System;
using System.Linq;
using System.Threading.Tasks;
using Trust_Finance.Services;
using Trust_Finance.Tests.Fixtures;
using Xunit;

namespace Trust_Finance.Tests.Services;

public class CategoryServiceTests
{
    private const int Ada = 1;
    private const int Bob = 2;

    private static CategoryService NewService()
        => new(DbContextFixture.CreateContext(Guid.NewGuid().ToString()));

    [Fact]
    public async Task Create_Should_Add_Category()
    {
        var service = NewService();

        var category = await service.CreateAsync("Alimentação", "alimentacao", Ada);

        category.Should().NotBeNull();
        category.Name.Should().Be("Alimentação");
        category.UserId.Should().Be(Ada);
    }

    [Fact]
    public async Task Create_Should_Fail_When_Slug_Is_Duplicated_For_The_Same_User()
    {
        var service = NewService();
        await service.CreateAsync("Alimentação", "alimentacao", Ada);

        Func<Task> action = async () =>
            await service.CreateAsync("Outra", "alimentacao", Ada);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Create_Should_Allow_The_Same_Slug_For_Different_Users()
    {
        var service = NewService();
        await service.CreateAsync("Alimentação", "alimentacao", Ada);

        var bobs = await service.CreateAsync("Alimentação", "alimentacao", Bob);

        bobs.UserId.Should().Be(Bob);
    }

    [Fact]
    public async Task GetAll_Should_Return_Only_The_Callers_Categories()
    {
        var service = NewService();
        await service.CreateAsync("Alimentação", "alimentacao", Ada);
        await service.CreateAsync("Aluguel", "aluguel", Bob);

        var categories = await service.GetAllAsync(Ada);

        categories.Should().ContainSingle().Which.Slug.Should().Be("alimentacao");
    }

    [Fact]
    public async Task GetById_Should_Not_Reach_Another_Users_Category()
    {
        var service = NewService();
        var adas = await service.CreateAsync("Alimentação", "alimentacao", Ada);

        (await service.GetByIdAsync(adas.Id, Bob)).Should().BeNull();
    }

    [Fact]
    public async Task Update_Should_Not_Reach_Another_Users_Category()
    {
        var service = NewService();
        var adas = await service.CreateAsync("Alimentação", "alimentacao", Ada);

        Func<Task> action = async () =>
            await service.UpdateAsync(adas.Id, "Sequestrada", "sequestrada", Bob);

        await action.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Delete_Should_Not_Reach_Another_Users_Category()
    {
        var service = NewService();
        var adas = await service.CreateAsync("Alimentação", "alimentacao", Ada);

        Func<Task> action = async () => await service.DeleteAsync(adas.Id, Bob);

        await action.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Update_Should_Allow_Keeping_The_Same_Slug()
    {
        var service = NewService();
        var category = await service.CreateAsync("Alimentação", "alimentacao", Ada);

        var updated = await service.UpdateAsync(category.Id, "Mercado", "alimentacao", Ada);

        updated.Name.Should().Be("Mercado");
    }
}
