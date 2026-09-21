using Microsoft.Playwright;

namespace TrustFinance.E2ETests.Infrastructure;

/// <summary>One Chromium instance shared by the whole E2E run; a page per test.</summary>
public sealed class BrowserFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;
    public LiveApp App { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        App = new LiveApp();
        await App.StartAsync();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    /// <summary>
    /// A fresh context per test, so cookies, localStorage and the chosen theme never leak
    /// from one test into the next.
    /// </summary>
    public async Task<IPage> NewPageAsync(int width = 1280, int height = 900)
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = width, Height = height },
            Locale = "pt-BR"
        });

        return await context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.CloseAsync();
        _playwright?.Dispose();
        await App.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class E2ECollection : ICollectionFixture<BrowserFixture>
{
    public const string Name = "e2e";
}
