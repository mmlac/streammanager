using System.Runtime.InteropServices;

namespace StreamManager.Core;

public sealed class AppPaths : IAppPaths
{
    private const string AppFolderName = "streammanager";

    public AppPaths()
        : this(ResolveAppDataRoot(Environment.GetEnvironmentVariable, GetOSPlatform))
    {
    }

    internal AppPaths(string appDataRoot)
    {
        AppDataRoot = appDataRoot;
        LogsDirectory = Path.Combine(AppDataRoot, "logs");
        CacheDirectory = Path.Combine(AppDataRoot, "cache");
        ConfigFilePath = Path.Combine(AppDataRoot, "config.json");
        PresetsFilePath = Path.Combine(AppDataRoot, "presets.json");
    }

    public string AppDataRoot { get; }
    public string LogsDirectory { get; }
    public string ConfigFilePath { get; }
    public string PresetsFilePath { get; }
    public string CacheDirectory { get; }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(CacheDirectory);
    }

    internal static string ResolveAppDataRoot(
        Func<string, string?> getEnv,
        Func<OSPlatform> getPlatform)
    {
        var platform = getPlatform();

        if (platform == OSPlatform.Windows)
        {
            var appData = getEnv("APPDATA");
            if (string.IsNullOrEmpty(appData))
            {
                appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }
            return Path.Combine(appData, AppFolderName);
        }

        if (platform == OSPlatform.OSX)
        {
            var home = getEnv("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", AppFolderName);
        }

        // Linux / other: follow XDG Base Directory spec.
        var xdg = getEnv("XDG_DATA_HOME");
        if (!string.IsNullOrEmpty(xdg))
        {
            return Path.Combine(xdg, AppFolderName);
        }
        var linuxHome = getEnv("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(linuxHome, ".local", "share", AppFolderName);
    }

    private static OSPlatform GetOSPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return OSPlatform.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return OSPlatform.OSX;
        return OSPlatform.Linux;
    }
}
