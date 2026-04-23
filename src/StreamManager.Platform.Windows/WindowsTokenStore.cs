using StreamManager.Core;

namespace StreamManager.Platform.Windows;

// Concrete implementation lands in slice 2 (Windows Credential Manager via CredWrite/CredRead).
public sealed class WindowsTokenStore : ITokenStore
{
    public Task<string?> GetRefreshTokenAsync(CancellationToken ct = default)
        => throw new NotImplementedException("Wired in slice 2");

    public Task SetRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
        => throw new NotImplementedException("Wired in slice 2");

    public Task DeleteRefreshTokenAsync(CancellationToken ct = default)
        => throw new NotImplementedException("Wired in slice 2");
}
