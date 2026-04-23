using Microsoft.Extensions.Logging;

namespace StreamManager.Core.Auth;

public sealed class YouTubeAuthenticator : IYouTubeAuthenticator
{
    private readonly IOAuthClientConfig _clientConfig;
    private readonly IGoogleOAuthFlow _flow;
    private readonly ITokenStore _tokenStore;
    private readonly IUserInfoClient _userInfo;
    private readonly IAuthState _state;
    private readonly ILogger<YouTubeAuthenticator> _log;

    public YouTubeAuthenticator(
        IOAuthClientConfig clientConfig,
        IGoogleOAuthFlow flow,
        ITokenStore tokenStore,
        IUserInfoClient userInfo,
        IAuthState state,
        ILogger<YouTubeAuthenticator> log)
    {
        _clientConfig = clientConfig;
        _flow = flow;
        _tokenStore = tokenStore;
        _userInfo = userInfo;
        _state = state;
        _log = log;
    }

    public async Task<AccountInfo> ConnectInteractiveAsync(CancellationToken ct)
    {
        var client = RequireConfiguredClient();

        _log.LogInformation("Starting interactive Google OAuth consent");
        var tokens = await _flow.AuthorizeInteractiveAsync(
            client, IYouTubeAuthenticator.RequiredScopes, ct).ConfigureAwait(false);

        await _tokenStore.SetRefreshTokenAsync(tokens.RefreshToken, ct).ConfigureAwait(false);
        var account = await _userInfo.FetchAsync(tokens.AccessToken, ct).ConfigureAwait(false);
        _state.SetConnected(account, tokens.AccessToken);
        _log.LogInformation("Connected to YouTube as {Email}", account.Email);
        return account;
    }

    public async Task<AccountInfo?> TrySilentReconnectAsync(CancellationToken ct)
    {
        if (!_clientConfig.IsConfigured)
        {
            return null;
        }

        var refresh = await _tokenStore.GetRefreshTokenAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(refresh))
        {
            return null;
        }

        var client = _clientConfig.Current!;
        try
        {
            var tokens = await _flow.RefreshAsync(client, refresh, ct).ConfigureAwait(false);
            // Rotate the persisted refresh token if Google sent a new one.
            if (!string.Equals(tokens.RefreshToken, refresh, StringComparison.Ordinal))
            {
                await _tokenStore.SetRefreshTokenAsync(tokens.RefreshToken, ct).ConfigureAwait(false);
            }
            var account = await _userInfo.FetchAsync(tokens.AccessToken, ct).ConfigureAwait(false);
            _state.SetConnected(account, tokens.AccessToken);
            return account;
        }
        catch (OAuthException ex)
        {
            // Refresh token rejected (revoked, expired, scope changed). Clear it
            // and fall through to disconnected; the user will reconnect via the
            // top bar or be prompted by the reauth modal on the next API call.
            _log.LogWarning(ex, "Silent reconnect failed; clearing stored refresh token");
            await _tokenStore.DeleteRefreshTokenAsync(ct).ConfigureAwait(false);
            _state.SetDisconnected();
            return null;
        }
        catch (UnauthorizedException ex)
        {
            _log.LogWarning(ex, "Silent reconnect: userinfo 401");
            await _tokenStore.DeleteRefreshTokenAsync(ct).ConfigureAwait(false);
            _state.SetDisconnected();
            return null;
        }
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        await _tokenStore.DeleteRefreshTokenAsync(ct).ConfigureAwait(false);
        _state.SetDisconnected();
        _log.LogInformation("Disconnected; refresh token removed from keychain");
    }

    private OAuthClient RequireConfiguredClient()
    {
        if (!_clientConfig.IsConfigured || _clientConfig.Current is null)
        {
            throw new InvalidOperationException(
                "OAuth client is not configured; complete the first-run setup screen first.");
        }
        return _clientConfig.Current;
    }
}
