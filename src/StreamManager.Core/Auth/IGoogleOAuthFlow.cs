namespace StreamManager.Core.Auth;

public interface IGoogleOAuthFlow
{
    Task<TokenSet> AuthorizeInteractiveAsync(
        OAuthClient client,
        IReadOnlyList<string> scopes,
        CancellationToken ct);

    Task<TokenSet> RefreshAsync(
        OAuthClient client,
        string refreshToken,
        CancellationToken ct);
}
