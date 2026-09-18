using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Trust_Finance.IntegrationTests.Infrastructure;

/// <summary>
/// Mints JWTs the same way <c>TokenService</c> does, but with knobs the API never exposes
/// (wrong signing key, expired lifetime) so the bearer configuration can be tested.
/// </summary>
public static class TestTokens
{
    public static string Create(
        string signingKey,
        int userId,
        string name = "Forged User",
        TimeSpan? lifetime = null)
    {
        var handler = new JwtSecurityTokenHandler();
        var expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, name)
            }),
            // NotBefore has to trail Expires, otherwise an already-expired token
            // cannot even be minted.
            NotBefore = expires.AddMinutes(-5),
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.ASCII.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256Signature)
        };

        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    public static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);
}
