using StreamManager.Core;
using Xunit;

namespace StreamManager.Core.Tests;

public class AppSettingsTests : IDisposable
{
    private readonly string _appDataRoot;
    private readonly AppPaths _paths;

    public AppSettingsTests()
    {
        _appDataRoot = Path.Combine(
            Path.GetTempPath(),
            $"sm-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_appDataRoot);
        _paths = new AppPaths(_appDataRoot);
        _paths.EnsureDirectoriesExist();
    }

    public void Dispose()
    {
        if (Directory.Exists(_appDataRoot))
        {
            try { Directory.Delete(_appDataRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public void DefaultsToUsWhenNoFileExists()
    {
        var settings = new AppSettings(_paths);
        Assert.Equal("US", settings.RegionCode);
    }

    [Fact]
    public async Task SetRegion_PersistsAndReloads()
    {
        var settings = new AppSettings(_paths);
        await settings.SetRegionCodeAsync("DE");

        Assert.Equal("DE", settings.RegionCode);

        var reloaded = new AppSettings(_paths);
        Assert.Equal("DE", reloaded.RegionCode);
    }

    [Fact]
    public async Task SetRegion_RaisesChangedOnlyWhenValueChanges()
    {
        var settings = new AppSettings(_paths);
        var count = 0;
        settings.Changed += (_, _) => count++;

        await settings.SetRegionCodeAsync("DE");
        await settings.SetRegionCodeAsync("DE");
        await settings.SetRegionCodeAsync("US");

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task SetRegion_RejectsEmpty()
    {
        var settings = new AppSettings(_paths);
        await Assert.ThrowsAsync<ArgumentException>(
            () => settings.SetRegionCodeAsync(""));
    }
}
