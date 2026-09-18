using FluentAssertions;
using TF.Services;
using TF.ViewModels;
using Trust_Finance.Tests.Fixtures;
using Xunit;

namespace Trust_Finance.Tests.Services;

public class AccountServiceTests
{
    [Fact]
    public async Task Register_Should_Create_User_With_Hashed_Password()
    {
        // Arrange
        var context = DbContextFixture.CreateContext(Guid.NewGuid().ToString());
        var service = new AccountService(context);

        var model = new RegisterUserViewModel
        {
            Name = "Teste",
            Email = "teste@gmail.com",
            Password = "teste123"
        };

        // Act
        var user = await service.RegisterAsync(model);

        // Assert
        user.Should().NotBeNull();
        user.Email.Should().Be("teste@gmail.com");
        user.PasswordHash.Should().NotBe("teste123");

        context.Users.Should().HaveCount(1);
    }

    [Fact]
    public async Task Register_Should_Fail_When_Email_Already_Exists()
    {
        // Arrange
        var context = DbContextFixture.CreateContext(Guid.NewGuid().ToString());
        var service = new AccountService(context);

        var firstModel = new RegisterUserViewModel
        {
            Name = "Teste",
            Email = "duplicado@gmail.com",
            Password = "123456"
        };

        var secondModel = new RegisterUserViewModel
        {
            Name = "Teste",
            Email = "duplicado@gmail.com",
            Password = "123456"
        };

        await service.RegisterAsync(firstModel);

        // Act
        Func<Task> action = async () => await service.RegisterAsync(secondModel);

        // Assert
        var ex = await action.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage("Email already registered");

        // Ensures a second user was not created
        context.Users.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("Louzoshi", "louzoshi")]
    [InlineData("João da Silva", "joao-da-silva")]
    [InlineData("  Ana   Maria  ", "ana-maria")]
    [InlineData("O'Brien & Co.", "o-brien-co")]
    [InlineData("!!!", "user")]
    public async Task Register_Should_Derive_The_Slug_From_The_Name(
        string name, string expected)
    {
        var context = DbContextFixture.CreateContext(Guid.NewGuid().ToString());
        var service = new AccountService(context);

        var user = await service.RegisterAsync(new RegisterUserViewModel
        {
            Name = name,
            Email = "slug@gmail.com",
            Password = "teste123"
        });

        user.Slug.Should().Be(expected);
        user.Image.Should().BeEmpty("an account is opened without supplying an avatar");
    }

    [Fact]
    public async Task Register_Should_Not_Repeat_A_Slug_Between_Two_Users()
    {
        var context = DbContextFixture.CreateContext(Guid.NewGuid().ToString());
        var service = new AccountService(context);

        var first = await service.RegisterAsync(new RegisterUserViewModel
        {
            Name = "Ana Maria",
            Email = "ana1@gmail.com",
            Password = "teste123"
        });
        var second = await service.RegisterAsync(new RegisterUserViewModel
        {
            Name = "Ana Maria",
            Email = "ana2@gmail.com",
            Password = "teste123"
        });

        first.Slug.Should().Be("ana-maria");
        second.Slug.Should().Be("ana-maria-2");
    }
}
