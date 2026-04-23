// CA1416 (platform compatibility) is suppressed for this file: each test is
// guarded with Skip.IfNot(OperatingSystem.IsXxx) before touching the
// platform-specific store, and the file as a whole only ever exercises one
// store per host. The analyzer doesn't recognise xunit's Skip pattern.
#pragma warning disable CA1416

using StreamManager.Core;
using Xunit;

namespace StreamManager.Core.Tests.Auth;

// Round-trips a refresh token through the live OS keychain on the host
// platform. Skipped on every other OS so the suite stays portable. Covers
// the "refresh token survives app restart" acceptance criterion by writing
// then reading via a *fresh* store instance (no in-memory cache).
public class PlatformTokenStoreTests
{
    [SkippableFact]
    public async Task Mac_RoundTripsRefreshToken()
    {
        Skip.IfNot(OperatingSystem.IsMacOS(), "macOS only");

        var store = new Platform.Mac.MacTokenStore();
        var token = $"sm-test-{Guid.NewGuid():N}";

        try
        {
            await store.SetRefreshTokenAsync(token);
            var back = await new Platform.Mac.MacTokenStore().GetRefreshTokenAsync();
            Assert.Equal(token, back);
        }
        finally
        {
            await store.DeleteRefreshTokenAsync();
        }

        Assert.Null(await new Platform.Mac.MacTokenStore().GetRefreshTokenAsync());
    }

    [SkippableFact]
    public async Task Windows_RoundTripsRefreshToken()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        var store = new Platform.Windows.WindowsTokenStore();
        var token = $"sm-test-{Guid.NewGuid():N}";

        try
        {
            await store.SetRefreshTokenAsync(token);
            var back = await new Platform.Windows.WindowsTokenStore().GetRefreshTokenAsync();
            Assert.Equal(token, back);
        }
        finally
        {
            await store.DeleteRefreshTokenAsync();
        }

        Assert.Null(await new Platform.Windows.WindowsTokenStore().GetRefreshTokenAsync());
    }

    [SkippableFact]
    public async Task Mac_DeleteIsIdempotent()
    {
        Skip.IfNot(OperatingSystem.IsMacOS(), "macOS only");

        var store = new Platform.Mac.MacTokenStore();
        await store.DeleteRefreshTokenAsync();
        await store.DeleteRefreshTokenAsync();
    }

    [SkippableFact]
    public async Task Windows_DeleteIsIdempotent()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        var store = new Platform.Windows.WindowsTokenStore();
        await store.DeleteRefreshTokenAsync();
        await store.DeleteRefreshTokenAsync();
    }
}
