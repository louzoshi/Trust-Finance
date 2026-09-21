using Microsoft.JSInterop;
using TrustFinance.Domain.Entities;

namespace TrustFinance.App.Services;

/// <summary>
/// The parts of the user's preferences the running page cares about, held for the life
/// of the circuit. App.razor already stamped them onto &lt;html&gt; for the first paint;
/// this keeps the server side in agreement and flips the attribute when one changes,
/// rather than reloading the page to find out.
/// </summary>
public class UiState(SettingsService settings, CurrentUser currentUser, IJSRuntime js)
{
    private UserSettings? _settings;

    public event Action? Changed;

    public bool PrivacyMode => _settings?.PrivacyMode ?? false;
    public Density Density => _settings?.Density ?? Density.Comfortable;
    public string? Accent => _settings?.AccentColor;
    public bool HasLiveMarketData => !string.IsNullOrWhiteSpace(_settings?.MarketDataToken);

    /// <summary>
    /// False until the circuit has acted on the user's chosen start page once. Clicking
    /// "Visão geral" afterwards has to actually show it, so the preference applies to
    /// landing on the app and not to every visit to "/".
    /// </summary>
    public bool StartPageHonoured { get; private set; }

    public void MarkStartPageHonoured() => StartPageHonoured = true;

    public async Task<UserSettings> LoadAsync()
    {
        _settings ??= await settings.GetAsync(await currentUser.IdAsync());
        return _settings;
    }

    /// <summary>Forgets the cached copy, so the next read sees what the settings screen saved.</summary>
    public void Invalidate()
    {
        _settings = null;
        Changed?.Invoke();
    }

    public async Task TogglePrivacyAsync()
    {
        var current = await LoadAsync();
        current.PrivacyMode = !current.PrivacyMode;

        await settings.SaveAsync(current);
        await js.InvokeVoidAsync("tfUi.setAttribute", "data-privacy", current.PrivacyMode ? "on" : "off");

        Changed?.Invoke();
    }
}
