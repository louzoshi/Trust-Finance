using TrustFinance.App.Services;
using TrustFinance.Domain.Entities;
using TrustFinance.Tests.Fixtures;

namespace TrustFinance.Tests.Services;

public class UserAccountServiceTests : IDisposable
{
    private readonly SqliteDatabase _db = new();
    private readonly FixedClock _clock = new(new DateOnly(2026, 9, 21));
    private readonly UserAccountService _service;

    public UserAccountServiceTests() => _service = new UserAccountService(_db, _clock);

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
    public async Task SignIn_Should_Return_The_User_For_The_Right_Password()
    {
        await _service.RegisterAsync("Carla", "carla@example.com", "segredo123");

        var outcome = await _service.SignInAsync("carla@example.com", "segredo123");

        outcome.Status.Should().Be(SignInStatus.Success);
        outcome.User!.Name.Should().Be("Carla");
    }

    [Theory]
    [InlineData("carla@example.com", "errada")]
    [InlineData("ninguem@example.com", "segredo123")]
    [InlineData("nao-e-email", "segredo123")]
    public async Task SignIn_Should_Refuse_A_Wrong_Email_Or_Password(string email, string password)
    {
        await _service.RegisterAsync("Carla", "carla@example.com", "segredo123");

        var outcome = await _service.SignInAsync(email, password);

        outcome.Status.Should().Be(SignInStatus.InvalidCredentials);
        outcome.User.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("qualquer")]
    public async Task SignIn_Should_Never_Accept_The_Passwordless_Local_Account(string password)
    {
        // The account Auth:Bypass owns is written straight to the table with no hash.
        // It has to stay unreachable from the login form, whatever is typed at it.
        await using (var db = _db.CreateDbContext())
        {
            db.Users.Add(User.Local("Local", AuthBypass.LocalEmail));
            await db.SaveChangesAsync();
        }

        (await _service.SignInAsync(AuthBypass.LocalEmail, password)).Status
            .Should().Be(SignInStatus.InvalidCredentials);
    }

    [Fact]
    public async Task SignIn_Should_Lock_The_Account_After_Five_Wrong_Passwords()
    {
        await _service.RegisterAsync("Carla", "carla@example.com", "segredo123");

        for (var attempt = 1; attempt < User.MaxFailedSignIns; attempt++)
            (await _service.SignInAsync("carla@example.com", "errada")).Status
                .Should().Be(SignInStatus.InvalidCredentials, "a conta só bloqueia na quinta tentativa");

        var fifth = await _service.SignInAsync("carla@example.com", "errada");

        fifth.Status.Should().Be(SignInStatus.LockedOut);
        fifth.LockoutRemaining.Should().BePositive();
    }

    [Fact]
    public async Task A_Locked_Account_Should_Refuse_Even_The_Right_Password()
    {
        await _service.RegisterAsync("Carla", "carla@example.com", "segredo123");

        for (var attempt = 0; attempt < User.MaxFailedSignIns; attempt++)
            await _service.SignInAsync("carla@example.com", "errada");

        // The point of the lockout: guessing right on the sixth try is worth nothing.
        (await _service.SignInAsync("carla@example.com", "segredo123")).Status
            .Should().Be(SignInStatus.LockedOut);
    }

    [Fact]
    public async Task A_Lockout_Should_End_On_Its_Own()
    {
        await _service.RegisterAsync("Carla", "carla@example.com", "segredo123");

        for (var attempt = 0; attempt < User.MaxFailedSignIns; attempt++)
            await _service.SignInAsync("carla@example.com", "errada");

        _clock.Today = _clock.Today.AddDays(1);

        (await _service.SignInAsync("carla@example.com", "segredo123")).Status
            .Should().Be(SignInStatus.Success);
    }

    [Fact]
    public async Task A_Successful_SignIn_Should_Forget_The_Failures_Before_It()
    {
        await _service.RegisterAsync("Carla", "carla@example.com", "segredo123");

        for (var attempt = 0; attempt < User.MaxFailedSignIns - 1; attempt++)
            await _service.SignInAsync("carla@example.com", "errada");

        (await _service.SignInAsync("carla@example.com", "segredo123")).Status.Should().Be(SignInStatus.Success);

        // Four failures, one success, then four more: the counter started over, so this is
        // still short of the ceiling.
        for (var attempt = 0; attempt < User.MaxFailedSignIns - 1; attempt++)
            (await _service.SignInAsync("carla@example.com", "errada")).Status
                .Should().Be(SignInStatus.InvalidCredentials);
    }

    [Fact]
    public async Task Profile_Should_Change_The_Name_And_The_Email()
    {
        var user = (await _service.RegisterAsync("Carla", "carla@example.com", "segredo123")).Value!;

        var result = await _service.UpdateProfileAsync(user.Id, "Carla Souza", " Carla.Souza@Example.com ");

        result.Success.Should().BeTrue();
        result.Value!.Name.Should().Be("Carla Souza");
        result.Value.Email.Should().Be("carla.souza@example.com");

        (await _service.SignInAsync("carla.souza@example.com", "segredo123")).Status.Should().Be(SignInStatus.Success);
    }

    [Fact]
    public async Task Profile_Should_Refuse_An_Email_Another_Account_Already_Uses()
    {
        var carla = (await _service.RegisterAsync("Carla", "carla@example.com", "segredo123")).Value!;
        await _service.RegisterAsync("Bruno", "bruno@example.com", "segredo123");

        var result = await _service.UpdateProfileAsync(carla.Id, "Carla", "bruno@example.com");

        result.Failed.Should().BeTrue();
        result.Messages.Should().ContainSingle().Which.Should().Contain("já está cadastrado");
    }

    [Fact]
    public async Task Profile_Should_Refuse_A_Name_That_Is_Too_Short()
    {
        var user = (await _service.RegisterAsync("Carla", "carla@example.com", "segredo123")).Value!;

        (await _service.UpdateProfileAsync(user.Id, "Jo", "carla@example.com")).Failed.Should().BeTrue();

        // Nothing was half-applied: the row is exactly as it was.
        var stored = (await _service.GetByIdAsync(user.Id))!;
        stored.Name.Should().Be("Carla");
    }

    [Fact]
    public async Task Password_Should_Change_When_The_Current_One_Is_Proved()
    {
        var user = (await _service.RegisterAsync("Carla", "carla@example.com", "segredo123")).Value!;

        (await _service.ChangePasswordAsync(user.Id, "segredo123", "outrasenha456")).Success.Should().BeTrue();

        (await _service.SignInAsync("carla@example.com", "outrasenha456")).Status.Should().Be(SignInStatus.Success);
        (await _service.SignInAsync("carla@example.com", "segredo123")).Status.Should().Be(SignInStatus.InvalidCredentials);
    }

    [Theory]
    [InlineData("errada", "outrasenha456", "Senha atual incorreta")]
    [InlineData("segredo123", "curta", "no mínimo")]
    [InlineData("segredo123", "segredo123", "diferente da atual")]
    public async Task Password_Should_Be_Refused_With_A_Reason(string current, string next, string reason)
    {
        var user = (await _service.RegisterAsync("Carla", "carla@example.com", "segredo123")).Value!;

        var result = await _service.ChangePasswordAsync(user.Id, current, next);

        result.Failed.Should().BeTrue();
        result.Messages.Should().ContainSingle().Which.Should().Contain(reason);

        // The old password still works, which is the part that matters.
        (await _service.SignInAsync("carla@example.com", "segredo123")).Status.Should().Be(SignInStatus.Success);
    }

    [Fact]
    public async Task Changing_The_Password_Should_Clear_A_Lockout()
    {
        var user = (await _service.RegisterAsync("Carla", "carla@example.com", "segredo123")).Value!;

        for (var attempt = 0; attempt < User.MaxFailedSignIns; attempt++)
            await _service.SignInAsync("carla@example.com", "errada");

        (await _service.ChangePasswordAsync(user.Id, "segredo123", "outrasenha456")).Success.Should().BeTrue();

        (await _service.SignInAsync("carla@example.com", "outrasenha456")).Status.Should().Be(SignInStatus.Success);
    }
}
