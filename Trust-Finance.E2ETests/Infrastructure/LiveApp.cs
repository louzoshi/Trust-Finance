using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace TrustFinance.E2ETests.Infrastructure;

/// <summary>
/// The published application, launched as its own process on a real port.
///
/// Not <c>WebApplicationFactory</c>: that serves through an in-memory transport a browser
/// cannot connect to, and hybrid Kestrel variants of it fight the base class. Starting the
/// actual binary is also the more honest end-to-end — it exercises the same executable a
/// user double-clicks, including startup, migrations and static asset serving.
/// </summary>
public sealed class LiveApp : IAsyncDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"tf-e2e-{Guid.NewGuid():N}.db");

    private Process? _process;

    public string BaseUrl { get; private set; } = string.Empty;

    public async Task StartAsync()
    {
        var port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var startInfo = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { AppAssemblyPath() },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.Environment["ASPNETCORE_URLS"] = BaseUrl;
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ConnectionStrings__Default"] = $"Data Source={_databasePath}";
        startInfo.Environment["Auth__Bypass"] = "true";

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the application under test.");

        // Drained so a chatty log cannot fill the pipe buffer and wedge the process.
        _ = _process.StandardOutput.ReadToEndAsync();
        _ = _process.StandardError.ReadToEndAsync();

        await WaitUntilReadyAsync();
    }

    private async Task WaitUntilReadyAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(60);

        while (DateTime.UtcNow < deadline)
        {
            if (_process!.HasExited)
                throw new InvalidOperationException($"The app exited during startup with code {_process.ExitCode}.");

            try
            {
                var response = await client.GetAsync(BaseUrl + "/login");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Not listening yet.
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"The app did not start listening on {BaseUrl} within 60s.");
    }

    /// <summary>The built app, in whichever configuration this test assembly was built in.</summary>
    private static string AppAssemblyPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        // .../Trust-Finance.E2ETests/bin/<config>/<tfm>/  ->  repo root
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Trust-Finance.sln")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("Could not locate the repository root from the test assembly.");

        var configuration = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}")
            ? "Release"
            : "Debug";

        var path = Path.Combine(directory.FullName,
            "Trust-Finance.App", "bin", configuration, "net10.0", "TrustFinance.dll");

        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"The app has not been built in {configuration}. Run 'dotnet build' first.", path);

        return path;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process?.Dispose();

        foreach (var path in new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" })
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException) { /* temp file; the OS will get it */ }
        }
    }
}
