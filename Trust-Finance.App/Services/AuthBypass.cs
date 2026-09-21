using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;

namespace TrustFinance.App.Services;

/// <summary>
/// Development shortcut: with <c>Auth:Bypass</c> on, every request arrives already signed
/// in as one local user and the sign-in screens are skipped.
///
/// This exists so the app can be worked on before authentication is a settled question.
/// Nothing about the real cookie scheme changes — it stays registered and stays the
/// default whenever the flag is off, so turning login back on is a configuration edit
/// rather than a rewrite. The flag defaults to off: a published build asks for a password.
/// </summary>
public sealed class AuthBypass
{
    public const string SchemeName = "LocalBypass";
    public const string LocalEmail = "local@trustfinance.app";

    public required bool Enabled { get; init; }

    /// <summary>The row every bypassed request owns. Resolved once at startup.</summary>
    public int UserId { get; private set; }
    public string Name { get; private set; } = "Local";

    /// <summary>
    /// Finds or creates the local user. The row carries no password hash, which is what
    /// keeps it unreachable from the login form even while the flag is on.
    /// </summary>
    public async Task EnsureUserAsync(TrustFinanceDbContext db)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == LocalEmail);

        if (user is null)
        {
            user = User.Local(Name, LocalEmail);
            db.Users.Add(user);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Somebody else created it between the read and the write — a second copy
                // of the app started against the same file. The unique index caught it,
                // and by now the winner's row is the answer. Losing this race must not
                // stop the app from booting.
                db.ChangeTracker.Clear();
                user = await db.Users.FirstAsync(u => u.Email == LocalEmail);
            }
        }

        UserId = user.Id;
        Name = user.Name;
    }
}

/// <summary>Authenticates every request as <see cref="AuthBypass"/>'s local user.</summary>
public sealed class BypassAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AuthBypass bypass) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var principal = UserAccountService.CreatePrincipal(
            bypass.UserId, bypass.Name, AuthBypass.LocalEmail, AuthBypass.SchemeName);

        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(principal, AuthBypass.SchemeName)));
    }
}
