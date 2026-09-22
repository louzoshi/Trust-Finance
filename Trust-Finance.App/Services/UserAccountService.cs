using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Notifications;
using TrustFinance.Domain.ValueObjects;

namespace TrustFinance.App.Services;

/// <summary>What happened on a sign-in attempt. Success carries the user; a lockout carries how long is left.</summary>
public enum SignInStatus
{
    Success,
    InvalidCredentials,
    LockedOut
}

public sealed record SignInOutcome(SignInStatus Status, User? User, TimeSpan LockoutRemaining)
{
    public static SignInOutcome Succeeded(User user) => new(SignInStatus.Success, user, TimeSpan.Zero);

    public static SignInOutcome Invalid { get; } = new(SignInStatus.InvalidCredentials, null, TimeSpan.Zero);

    public static SignInOutcome Locked(TimeSpan remaining) => new(SignInStatus.LockedOut, null, remaining);
}

public class UserAccountService(IDbContextFactory<TrustFinanceDbContext> factory, TimeProvider clock)
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    public const int MinPasswordLength = 8;

    public async Task<Result<User>> RegisterAsync(string name, string email, string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            return Result<User>.Fail(nameof(password), $"A senha deve ter no mínimo {MinPasswordLength} caracteres");

        // Built with an empty hash and filled below: hashing is an infrastructure
        // concern, so the domain never sees a plain password.
        var user = new User(name, email, string.Empty);
        if (user.IsInvalid)
            return Result<User>.Fail(user);

        await using var db = await factory.CreateDbContextAsync();

        if (await db.Users.AnyAsync(u => u.Email == user.Email))
            return Result<User>.Fail(nameof(User.Email), "Este e-mail já está cadastrado");

        user.SetPasswordHash(_passwordHasher.HashPassword(user, password));

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return Result<User>.Ok(user);
    }

    /// <summary>
    /// The outcome of one sign-in attempt. Wrong email and wrong password are
    /// indistinguishable on purpose.
    ///
    /// Five consecutive failures lock the account for fifteen minutes, which is what turns
    /// an offline-speed password guess into a two-attempts-per-hour one. A locked account
    /// says so rather than repeating "invalid": the person who mistyped their own password
    /// needs to know why the right one stopped working, and the attacker who hit the ceiling
    /// already knows they did. The email-enumeration that follows from it is bounded by the
    /// per-address throttle in <see cref="SignInThrottle"/>, which does not care whether the
    /// account exists.
    /// </summary>
    public async Task<SignInOutcome> SignInAsync(string email, string password)
    {
        var address = new Email(email);
        if (address.IsInvalid)
            return SignInOutcome.Invalid;

        await using var db = await factory.CreateDbContextAsync();

        // Tracked, not AsNoTracking: a failed attempt is written back.
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == address.Value);
        if (user is null)
            return SignInOutcome.Invalid;

        var now = clock.GetUtcNow();
        if (user.IsLockedOut(now))
            return SignInOutcome.Locked(user.LockoutRemaining(now));

        // A user with no hash was not created by registration — it is the local account the
        // Auth:Bypass flag owns. There is no password that should let anyone in as it.
        if (user.IsPasswordless)
            return SignInOutcome.Invalid;

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (verification == PasswordVerificationResult.Failed)
        {
            user.RegisterFailedSignIn(now);
            await db.SaveChangesAsync();

            return user.IsLockedOut(now)
                ? SignInOutcome.Locked(user.LockoutRemaining(now))
                : SignInOutcome.Invalid;
        }

        // The hasher asks for a rehash when its own parameters have moved on since the
        // password was set. Taking it here is free: the plain password is in hand exactly
        // once, right now.
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.SetPasswordHash(_passwordHasher.HashPassword(user, password));

        user.RegisterSuccessfulSignIn();
        await db.SaveChangesAsync();

        return SignInOutcome.Succeeded(user);
    }

    public async Task<User?> GetByIdAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
    }

    /// <summary>Renames the account and moves its email, both checked the way registration checks them.</summary>
    public async Task<Result<User>> UpdateProfileAsync(int userId, string name, string email)
    {
        await using var db = await factory.CreateDbContextAsync();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return Result<User>.Fail(nameof(User), "Conta não encontrada");

        var address = new Email(email);
        if (address.IsValid && await db.Users.AnyAsync(u => u.Email == address.Value && u.Id != userId))
            return Result<User>.Fail(nameof(User.Email), "Este e-mail já está cadastrado");

        user.Rename(name);
        user.ChangeEmail(email);

        if (user.IsInvalid)
            return Result<User>.Fail(user);

        await db.SaveChangesAsync();
        return Result<User>.Ok(user);
    }

    /// <summary>
    /// Replaces the password, once the current one has been proved. Proving it matters even
    /// though the session is already authenticated: a cookie left open on a borrowed machine
    /// would otherwise be enough to take the account over for good.
    /// </summary>
    public async Task<Result> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < MinPasswordLength)
            return Result.Fail(nameof(newPassword), $"A nova senha deve ter no mínimo {MinPasswordLength} caracteres");

        await using var db = await factory.CreateDbContextAsync();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return Result.Fail(nameof(User), "Conta não encontrada");

        // The bypass account has no password to replace, and giving it one would make it
        // reachable from the login form.
        if (user.IsPasswordless)
            return Result.Fail(nameof(currentPassword), "Esta conta não usa senha");

        if (_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            return Result.Fail(nameof(currentPassword), "Senha atual incorreta");

        if (currentPassword == newPassword)
            return Result.Fail(nameof(newPassword), "A nova senha precisa ser diferente da atual");

        user.SetPasswordHash(_passwordHasher.HashPassword(user, newPassword));
        if (user.IsInvalid)
            return Result.Fail(user);

        // Someone who just proved the current password is not the attacker the lockout was
        // put there for.
        user.RegisterSuccessfulSignIn();

        await db.SaveChangesAsync();
        return Result.Ok();
    }

    public static ClaimsPrincipal CreatePrincipal(User user, string authenticationScheme) =>
        CreatePrincipal(user.Id, user.Name, user.Email, authenticationScheme);

    /// <summary>
    /// The claims overload, for the bypass handler: it has an id and a name but no row
    /// to load, and constructing a throwaway User just to read three fields back off it
    /// would mean building an entity that does not represent anything.
    /// </summary>
    public static ClaimsPrincipal CreatePrincipal(int id, string name, string email, string authenticationScheme)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Email, email),
        ], authenticationScheme);
        return new ClaimsPrincipal(identity);
    }

}
