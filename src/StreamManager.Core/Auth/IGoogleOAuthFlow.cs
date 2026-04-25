namespace StreamManager.Core.Auth;

public interface IGoogleOAuthFlow
{
    Task<TokenSet> AuthorizeInteractiveAsync(
        OAuthClient client,
        IReadOnlyList<string> scopes,
        CancellationToken ct,
        Action<string>? onAuthUrlReady = null);

    Task<TokenSet> RefreshAsync(
        OAuthClient client,
        string refreshToken,
        CancellationToken ct);
}
