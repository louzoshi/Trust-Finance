namespace TrustFinance.Data;

public static class DatabaseLocation
{
    public const string FileName = "trustfinance.db";

    /// <summary>
    /// The per-user data folder: <c>~/.local/share/TrustFinance</c> on Linux,
    /// <c>%LOCALAPPDATA%\TrustFinance</c> on Windows. Created on demand so a fresh
    /// install works without any setup step.
    /// </summary>
    public static string DefaultDirectory()
    {
        var root = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);
        return Path.Combine(root, "TrustFinance");
    }

    public static string DefaultPath()
    {
        var directory = DefaultDirectory();
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, FileName);
    }

    public static string ConnectionString(string path) => $"Data Source={path}";
}
