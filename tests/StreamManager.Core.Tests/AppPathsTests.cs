using System.Runtime.InteropServices;
using StreamManager.Core;
using Xunit;

namespace StreamManager.Core.Tests;

public class AppPathsTests
{
    [Fact]
    public void ResolvesWindowsAppDataShape()
    {
        var appData = Path.Combine(Path.GetTempPath(), "mock-appdata");
        var resolved = AppPaths.ResolveAppDataRoot(
            getEnv: name => name == "APPDATA" ? appData : null,
            getPlatform: () => OSPlatform.Windows);

        Assert.Equal(Path.Combine(appData, "streammanager"), resolved);
    }

    [Fact]
    public void ResolvesMacAppDataShape()
    {
        var home = Path.Combine(Path.GetTempPath(), "mock-home");
        var resolved = AppPaths.ResolveAppDataRoot(
            getEnv: name => name == "HOME" ? home : null,
            getPlatform: () => OSPlatform.OSX);

        Assert.Equal(
            Path.Combine(home, "Library", "Application Support", "streammanager"),
            resolved);
    }

    [Fact]
    public void ResolvesLinuxAppDataShapeViaXdg()
    {
        var xdg = Path.Combine(Path.GetTempPath(), "mock-xdg");
        var resolved = AppPaths.ResolveAppDataRoot(
            getEnv: name => name == "XDG_DATA_HOME" ? xdg : null,
            getPlatform: () => OSPlatform.Linux);

        Assert.Equal(Path.Combine(xdg, "streammanager"), resolved);
    }

    [Fact]
    public void EnsureDirectoriesExistIsIdempotent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sm-test-{Guid.NewGuid():N}");
        try
        {
            var paths = new AppPaths(root);

            paths.EnsureDirectoriesExist();
            paths.EnsureDirectoriesExist(); // second call must not throw

            Assert.True(Directory.Exists(paths.AppDataRoot));
            Assert.True(Directory.Exists(paths.LogsDirectory));
            Assert.True(Directory.Exists(paths.CacheDirectory));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
