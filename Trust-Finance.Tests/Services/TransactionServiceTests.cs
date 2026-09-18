using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using TF.Data;
using TF.Models;
using Trust_Finance.Services;
using Trust_Finance.Tests.Fixtures;
using Xunit;

namespace Trust_Finance.Tests.Services;

public class TransactionServiceTests
{
    private static async Task<User> AddUserAsync(TFDataContext context, string email)
    {
        var user = new User
        {
            Name = "Teste",
            Email = email,
            PasswordHash = "hash",
            Role = "user"
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    private static async Task<Category> AddCategoryAsync(
        TFDataContext context, int userId, string slug = "alimentacao")
    {
        var category = new Category
        {
            Name = "Alimentação",
            Slug = slug,
            UserId = userId
        };

        context.Categories.Add(category);
        await context.SaveChangesAsync();

        return category;
    }

    [Fact]
    public async Task Create_Should_Add_Transaction_For_User()
    {
        // Arrange
        var context = DbContextFixture.CreateContext(Guid.NewGuid().ToString());
        var user = await AddUserAsync(context, "teste@gmail.com");
        var category = await AddCategoryAsync(context, user.Id);

        var service = new TransactionService(context);

        // Act
        var transaction = await service.CreateAsync(
            description: "Almoço",
            amount: 50,
            date: DateTime.UtcNow,
            categoryId: category.Id,
            userId: user.Id
        );

        // Assert
        transaction.Should().NotBeNull();
        transaction.Description.Should().Be("Almoço");
        transaction.Amount.Should().Be(50);
        transaction.UserId.Should().Be(user.Id);

        context.Transactions.Count().Should().Be(1);
    }

    [Fact]
    public async Task Create_Should_Reject_A_Category_Owned_By_Somebody_Else()
    {
        var context = DbContextFixture.CreateContext(Guid.NewGuid().ToString());
        var ada = await AddUserAsync(context, "ada@gmail.com");
        var bob = await AddUserAsync(context, "bob@gmail.com");
        var adasCategory = await AddCategoryAsync(context, ada.Id);

        var service = new TransactionService(context);

        Func<Task> action = async () => await service.CreateAsync(
            description: "Sequestro",
            amount: 50,
            date: DateTime.UtcNow,
            categoryId: adasCategory.Id,
            userId: bob.Id
        );

        await action.Should().ThrowAsync<InvalidOperationException>();
        context.Transactions.Count().Should().Be(0);
    }
}
