using StreamManager.Core;
using StreamManager.Core.Auth;
using Xunit;

namespace StreamManager.Core.Tests.Auth;

public class OAuthClientConfigTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), $"sm-cfg-{Guid.NewGuid():N}");

    private AppPaths Paths => new(_root);

    [Fact]
    public void MissingFile_IsConfigured_False()
    {
        var cfg = new OAuthClientConfig(Paths);

        Assert.False(cfg.IsConfigured);
        Assert.Null(cfg.Current);
    }

    [Fact]
    public async Task SaveAndReload_RoundTrip()
    {
        var cfg = new OAuthClientConfig(Paths);
        await cfg.SaveAsync(new OAuthClient("client-id-123", "secret-xyz"));

        var reloaded = new OAuthClientConfig(Paths);
        Assert.True(reloaded.IsConfigured);
        Assert.Equal("client-id-123", reloaded.Current!.ClientId);
        Assert.Equal("secret-xyz", reloaded.Current!.ClientSecret);
    }

    [Fact]
    public void EmptyFile_DoesNotThrow_AndIsNotConfigured()
    {
        var paths = Paths;
        paths.EnsureDirectoriesExist();
        File.WriteAllText(paths.ConfigFilePath, string.Empty);

        var cfg = new OAuthClientConfig(paths);
        Assert.False(cfg.IsConfigured);
    }

    [Fact]
    public void MalformedFile_DoesNotThrow_AndIsNotConfigured()
    {
        var paths = Paths;
        paths.EnsureDirectoriesExist();
        File.WriteAllText(paths.ConfigFilePath, "{ not valid json");

        var cfg = new OAuthClientConfig(paths);
        Assert.False(cfg.IsConfigured);
    }

    [Fact]
    public async Task Save_DoesNotClobberUnknownFields()
    {
        var paths = Paths;
        paths.EnsureDirectoriesExist();
        File.WriteAllText(
            paths.ConfigFilePath,
            "{ \"oAuthClientId\": \"old\", \"oAuthClientSecret\": \"old\", \"logLevel\": \"Debug\" }");

        var cfg = new OAuthClientConfig(paths);
        await cfg.SaveAsync(new OAuthClient("new", "new"));

        var json = File.ReadAllText(paths.ConfigFilePath);
        Assert.Contains("logLevel", json);
        Assert.Contains("Debug", json);
    }

    [Fact]
    public async Task Save_RaisesChangedEvent()
    {
        var cfg = new OAuthClientConfig(Paths);
        var raised = 0;
        cfg.Changed += (_, _) => raised++;

        await cfg.SaveAsync(new OAuthClient("a", "b"));

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task Save_WithBlankFields_TreatedAsNotConfigured()
    {
        var cfg = new OAuthClientConfig(Paths);
        await cfg.SaveAsync(new OAuthClient("   ", "  "));

        var reloaded = new OAuthClientConfig(Paths);
        Assert.False(reloaded.IsConfigured);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            try { Directory.Delete(_root, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
