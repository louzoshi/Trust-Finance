using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TF.Data;
using TF.Models;
using TF.ViewModels;

namespace TF.Services;

public class AccountService
{
    private readonly TFDataContext _context;
    private readonly PasswordHasher<User> _passwordHasher;

    public AccountService(TFDataContext context)
    {
        _context = context;
        _passwordHasher = new PasswordHasher<User>();
    }

    public async Task<User> RegisterAsync(RegisterUserViewModel model)
    {
        // Normalize the email to avoid false negatives (surrounding spaces, etc.)
        var email = model.Email?.Trim();

        // Business rule: email must be unique
        var emailAlreadyExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(x => x.Email == email);

        if (emailAlreadyExists)
            throw new InvalidOperationException("Email already registered");

        var user = new User
        {
            Name = model.Name,
            Email = email ?? string.Empty,
            Image = string.Empty,
            Slug = await UniqueSlugFromAsync(model.Name),
            Role = "user"
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }


    /// <summary>
    /// Builds the account's slug from its display name: lowercase, accents stripped,
    /// anything else collapsed to a single hyphen. A name that is all punctuation
    /// leaves nothing usable, so it falls back to "user".
    /// </summary>
    /// <remarks>
    /// There is no unique index on Users.Slug, so a repeat would not be an error --
    /// it would just be confusing. A counter is appended until the slug is free.
    /// </remarks>
    private async Task<string> UniqueSlugFromAsync(string name)
    {
        var slug = Slugify(name);
        if (slug.Length == 0)
            slug = "user";

        var candidate = slug;
        for (var n = 2; await _context.Users.AsNoTracking()
                 .AnyAsync(u => u.Slug == candidate); n++)
        {
            candidate = $"{slug}-{n}";
        }

        return candidate;
    }

    private static string Slugify(string value)
    {
        // FormD splits "ç" into "c" + cedilla, so dropping the combining marks
        // leaves the plain letter behind.
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsAsciiLetterOrDigit(ch))
                builder.Append(ch);
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }

        return builder.ToString().Trim('-');
    }

    public async Task<string> LoginAsync(LoginViewModel model, TokenService tokenService)
    {
        var email = model.Email?.Trim();

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == email);

        if (user == null)
            throw new UnauthorizedAccessException("Invalid username or password");

        var result = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, model.Password);

        if (result == PasswordVerificationResult.Failed)
            throw new UnauthorizedAccessException("Invalid username or password");

        return tokenService.GenerateToken(user);
    }
}
