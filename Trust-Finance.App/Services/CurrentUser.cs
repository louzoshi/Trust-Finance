using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace TrustFinance.App.Services;

/// <summary>
/// The signed-in user as seen from inside an interactive circuit, where there is no
/// HttpContext. Resolved once per call from the cascading authentication state.
/// </summary>
public class CurrentUser(AuthenticationStateProvider authenticationState)
{
    public async Task<int> IdAsync()
    {
        var state = await authenticationState.GetAuthenticationStateAsync();
        return state.User.GetUserId();
    }

    public async Task<string> NameAsync()
    {
        var state = await authenticationState.GetAuthenticationStateAsync();
        return state.User.Identity?.Name ?? string.Empty;
    }
}

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out var id))
            throw new InvalidOperationException("The current principal has no user id claim.");
        return id;
    }
}
