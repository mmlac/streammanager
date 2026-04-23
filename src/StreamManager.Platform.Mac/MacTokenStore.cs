using StreamManager.Core;

namespace StreamManager.Platform.Mac;

// Concrete implementation lands in slice 2 (macOS Keychain via Security framework).
public sealed class MacTokenStore : ITokenStore
{
    public Task<string?> GetRefreshTokenAsync(CancellationToken ct = default)
        => throw new NotImplementedException("Wired in slice 2");

    public Task SetRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
        => throw new NotImplementedException("Wired in slice 2");

    public Task DeleteRefreshTokenAsync(CancellationToken ct = default)
        => throw new NotImplementedException("Wired in slice 2");
}
