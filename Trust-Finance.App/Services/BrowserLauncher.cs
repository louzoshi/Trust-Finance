using System.Diagnostics;

namespace TrustFinance.App.Services;

public static class BrowserLauncher
{
    /// <summary>
    /// Opens <paramref name="url"/> in the default browser. Best effort: on a headless
    /// box or a locked-down desktop this fails, and the log line with the URL is the
    /// fallback, so nothing here is allowed to take the app down.
    /// </summary>
    public static void TryOpen(string url, ILogger logger)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", url);
            else
                Process.Start("xdg-open", url);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not open a browser automatically. Open {Url} manually.", url);
        }
    }
}
