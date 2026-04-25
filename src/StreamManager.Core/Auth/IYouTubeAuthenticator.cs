namespace StreamManager.Core.Auth;

public interface IYouTubeAuthenticator
{
    // Required scopes to fully cover the YouTube Data API v3 surface in design.md §4
    // (broadcast updates + thumbnail upload) plus the `email` scope so we can resolve
    // the connected account's address for the top-bar UI.
    public static readonly IReadOnlyList<string> RequiredScopes = new[]
    {
        "openid",
        "email",
        "https://www.googleapis.com/auth/youtube",
        "https://www.googleapis.com/auth/youtube.force-ssl",
        "https://www.googleapis.com/auth/youtube.upload",
    };

    Task<AccountInfo> ConnectInteractiveAsync(CancellationToken ct, Action<string>? onAuthUrlReady = null);
    Task<AccountInfo?> TrySilentReconnectAsync(CancellationToken ct);
    Task DisconnectAsync(CancellationToken ct);
}
