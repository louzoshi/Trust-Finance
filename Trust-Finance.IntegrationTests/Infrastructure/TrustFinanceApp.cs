using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TrustFinance.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real application — the same Program.cs that runs in production — against a
/// throwaway SQLite file. Nothing is mocked: requests go through the real middleware
/// pipeline, the real DI graph, the real EF provider and the real migrations.
/// </summary>
public class TrustFinanceApp : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"tf-it-{Guid.NewGuid():N}.db");

    /// <summary>Whether the app under test skips the sign-in screens. Set before the first request.</summary>
    public bool BypassAuth { get; init; } = true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting rather than ConfigureAppConfiguration: Program.cs reads Auth:Bypass
        // while the builder is being constructed, which is before the configuration
        // callbacks run. Only host settings are visible that early.
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_databasePath}");
        builder.UseSetting("Auth:Bypass", BypassAuth ? "true" : "false");
    }

    /// <summary>A client that does not chase redirects, so a 302 can be asserted on.</summary>
    public HttpClient CreatePlainClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        TryDelete(_databasePath);

        // SQLite runs in WAL mode, which leaves two companions behind.
        TryDelete(_databasePath + "-wal");
        TryDelete(_databasePath + "-shm");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // A temp file the OS will clean up. Not worth failing a test run over.
        }
    }
}
