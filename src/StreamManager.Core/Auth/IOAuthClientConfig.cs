namespace StreamManager.Core.Auth;

public interface IOAuthClientConfig
{
    bool IsConfigured { get; }
    OAuthClient? Current { get; }

    Task SaveAsync(OAuthClient client, CancellationToken ct = default);
    Task ReloadAsync(CancellationToken ct = default);

    event EventHandler? Changed;
}
