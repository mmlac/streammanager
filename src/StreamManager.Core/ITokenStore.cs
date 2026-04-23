namespace StreamManager.Core;

public interface ITokenStore
{
    Task<string?> GetRefreshTokenAsync(CancellationToken ct = default);

    Task SetRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    Task DeleteRefreshTokenAsync(CancellationToken ct = default);
}
