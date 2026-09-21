using Microsoft.JSInterop;

namespace TrustFinance.App.Services;

/// <summary>
/// Light or dark, as chosen on this browser. The choice itself lives in localStorage
/// (see wwwroot/theme.js, which applies it before first paint); this is the server-side
/// mirror so components that need concrete colours — charts — can follow it.
/// </summary>
public class ThemeState(IJSRuntime js)
{
    public string Current { get; private set; } = "light";
    public bool IsDark => Current == "dark";

    public event Action? Changed;

    public async Task SyncAsync()
    {
        var theme = await js.InvokeAsync<string>("tfTheme.current");
        Set(theme);
    }

    public async Task ToggleAsync()
    {
        var theme = await js.InvokeAsync<string>("tfTheme.toggle");
        Set(theme);
    }

    private void Set(string theme)
    {
        if (theme == Current) return;
        Current = theme;
        Changed?.Invoke();
    }
}
