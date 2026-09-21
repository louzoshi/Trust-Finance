using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Notifications;
using TrustFinance.Domain.ValueObjects;

namespace TrustFinance.App.Services;

public class UserAccountService(IDbContextFactory<TrustFinanceDbContext> factory)
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

    /// <summary>The user matching the credentials, or null. Wrong email and wrong password are indistinguishable on purpose.</summary>
    public async Task<User?> ValidateCredentialsAsync(string email, string password)
    {
        var address = new Email(email);
        if (address.IsInvalid)
            return null;

        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == address.Value);
        if (user is null)
            return null;

        // A user with no hash was not created by registration — it is the local account the
        // Auth:Bypass flag owns. There is no password that should let anyone in as it.
        if (user.IsPasswordless)
            return null;

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : user;
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
