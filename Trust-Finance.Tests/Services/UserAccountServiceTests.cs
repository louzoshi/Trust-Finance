using TrustFinance.App.Services;
using TrustFinance.Tests.Fixtures;

namespace TrustFinance.Tests.Services;

public class UserAccountServiceTests : IDisposable
{
    private readonly SqliteDatabase _db = new();
    private readonly UserAccountService _service;

    public UserAccountServiceTests() => _service = new UserAccountService(_db);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Register_Should_Store_A_Hash_Not_The_Password()
    {
        var result = await _service.RegisterAsync("Carla", "carla@example.com", "segredo123");

        result.Success.Should().BeTrue();
        result.Value!.Id.Should().BePositive();
        result.Value.PasswordHash.Should().NotBeNullOrEmpty().And.NotContain("segredo123");
    }

    [Fact]
    public async Task Register_Should_Normalize_The_Email()
    {
        var result = await _service.RegisterAsync("Carla", "  Carla@Example.COM ", "segredo123");

        result.Value!.Email.Should().Be("carla@example.com");
    }

    [Fact]
    public async Task Register_Should_Fail_When_Email_Already_Exists()
    {
        await _service.RegisterAsync("Carla", "carla@example.com", "segredo123");

        var result = await _service.RegisterAsync("Outra Carla", "CARLA@example.com", "outrasenha");

        result.Failed.Should().BeTrue();
        result.Messages.Should().ContainSingle().Which.Should().Contain("já está cadastrado");
    }

    [Theory]
    [InlineData("nao-e-email", "segredo123", "Email")]
    [InlineData("carla@example.com", "curta", "password")]
    [InlineData("carla@example.com", "", "password")]
    public async Task Register_Should_Reject_Bad_Input_With_A_Notification(string email, string password, string key)
    {
        var result = await _service.RegisterAsync("Carla", email, password);

        result.Failed.Should().BeTrue();
        result.Notifications.Should().Contain(n => n.Key == key);
    }

    [Fact]
    public async Task Register_Should_Reject_A_Name_That_Is_Too_Short()
    {
        var result = await _service.RegisterAsync("Jo", "jo@example.com", "segredo123");

        result.Failed.Should().BeTrue();
        result.Notifications.Should().Contain(n => n.Key == "Name");
    }

    [Fact]
    public async Task ValidateCredentials_Should_Return_The_User_For_The_Right_Password()
    {
        await _service.RegisterAsync("Carla", "carla@example.com", "segredo123");

        var user = await _service.ValidateCredentialsAsync("carla@example.com", "segredo123");

        user.Should().NotBeNull();
        user!.Name.Should().Be("Carla");
    }

    [Theory]
    [InlineData("carla@example.com", "errada")]
    [InlineData("ninguem@example.com", "segredo123")]
    [InlineData("nao-e-email", "segredo123")]
    public async Task ValidateCredentials_Should_Return_Null_For_Wrong_Email_Or_Password(string email, string password)
    {
        await _service.RegisterAsync("Carla", "carla@example.com", "segredo123");

        (await _service.ValidateCredentialsAsync(email, password)).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("qualquer")]
    public async Task ValidateCredentials_Should_Never_Accept_The_Passwordless_Local_Account(string password)
    {
        // The account Auth:Bypass owns is written straight to the table with no hash.
        // It has to stay unreachable from the login form, whatever is typed at it.
        await using (var db = _db.CreateDbContext())
        {
            db.Users.Add(global::TrustFinance.Domain.Entities.User.Local("Local", AuthBypass.LocalEmail));
            await db.SaveChangesAsync();
        }

        (await _service.ValidateCredentialsAsync(AuthBypass.LocalEmail, password)).Should().BeNull();
    }
}
