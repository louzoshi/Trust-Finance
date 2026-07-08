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
            Image = model.Image,
            Slug = model.Slug,
            Role = "user"
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
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
