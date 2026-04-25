using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace StreamManager.Core.Auth;

// Hand-rolled installed-app OAuth: PKCE (S256), system browser, loopback
// redirect on an ephemeral 127.0.0.1 port, Google's standard token endpoint.
//
// design.md §3 lists `Google.Apis.Auth` for this; we do the dance directly
// here to keep the dependency surface tight (one HttpClient, no transitive
// Newtonsoft.Json) and to keep IGoogleOAuthFlow trivially mockable in tests.
// The mechanics are identical to the GoogleWebAuthorizationBroker path.
public sealed class GoogleOAuthFlow : IGoogleOAuthFlow
{
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    private readonly IBrowserLauncher _browser;
    private readonly HttpClient _http;
    private readonly TimeProvider _time;

    public GoogleOAuthFlow(IBrowserLauncher browser, HttpClient http, TimeProvider? time = null)
    {
        _browser = browser;
        _http = http;
        _time = time ?? TimeProvider.System;
    }

    public async Task<TokenSet> AuthorizeInteractiveAsync(
        OAuthClient client,
        IReadOnlyList<string> scopes,
        CancellationToken ct,
        Action<string>? onAuthUrlReady = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(scopes);

        var verifier = GenerateCodeVerifier();
        var challenge = ComputeS256Challenge(verifier);
        var state = GenerateRandomToken(16);

        var (listener, port) = StartLoopbackListener();
        try
        {
            var redirectUri = $"http://127.0.0.1:{port}/";
            var authUrl = BuildAuthorizationUrl(client.ClientId, redirectUri, scopes, state, challenge);
            onAuthUrlReady?.Invoke(authUrl);
            _browser.Launch(authUrl);

            var ctx = await listener.GetContextAsync().WaitAsync(ct).ConfigureAwait(false);
            var query = ctx.Request.Url?.Query ?? string.Empty;
            var (code, returnedState, error) = ParseCallback(query);
            await WriteResponseAsync(ctx.Response, error is null ? SuccessHtml : FailHtml).ConfigureAwait(false);

            if (error is not null)
            {
                throw new OAuthException($"Authorization rejected by user or server: {error}");
            }
            if (returnedState != state)
            {
                throw new OAuthException("Authorization state mismatch (possible CSRF).");
            }
            if (string.IsNullOrEmpty(code))
            {
                throw new OAuthException("Authorization response did not include a code.");
            }

            return await ExchangeCodeAsync(client, code, redirectUri, verifier, ct).ConfigureAwait(false);
        }
        finally
        {
            try { listener.Stop(); } catch { /* best-effort */ }
            ((IDisposable)listener).Dispose();
        }
    }

    public async Task<TokenSet> RefreshAsync(
        OAuthClient client,
        string refreshToken,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrEmpty(refreshToken);

        var body = new Dictionary<string, string>
        {
            ["client_id"] = client.ClientId,
            ["client_secret"] = client.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        };

        using var resp = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(body), ct)
            .ConfigureAwait(false);
        var payload = await resp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
            .ConfigureAwait(false)
            ?? throw new OAuthException("Empty refresh response.");

        if (!resp.IsSuccessStatusCode || string.IsNullOrEmpty(payload.AccessToken))
        {
            throw new OAuthException(
                $"Refresh failed: {payload.Error ?? resp.StatusCode.ToString()} {payload.ErrorDescription}".Trim());
        }

        // Google omits refresh_token on refresh responses; reuse the supplied one.
        return new TokenSet(
            payload.AccessToken,
            payload.RefreshToken ?? refreshToken,
            _time.GetUtcNow().AddSeconds(payload.ExpiresIn ?? 0));
    }

    private async Task<TokenSet> ExchangeCodeAsync(
        OAuthClient client, string code, string redirectUri, string verifier, CancellationToken ct)
    {
        var body = new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = client.ClientId,
            ["client_secret"] = client.ClientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = verifier,
        };

        using var resp = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(body), ct)
            .ConfigureAwait(false);
        var payload = await resp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
            .ConfigureAwait(false)
            ?? throw new OAuthException("Empty token response.");

        if (!resp.IsSuccessStatusCode
            || string.IsNullOrEmpty(payload.AccessToken)
            || string.IsNullOrEmpty(payload.RefreshToken))
        {
            throw new OAuthException(
                $"Token exchange failed: {payload.Error ?? resp.StatusCode.ToString()} {payload.ErrorDescription}".Trim());
        }

        return new TokenSet(
            payload.AccessToken,
            payload.RefreshToken,
            _time.GetUtcNow().AddSeconds(payload.ExpiresIn ?? 0));
    }

    // Try a handful of ephemeral ports; if the first allocation races with
    // another process, fall through to the next one. Covers the
    // "loopback port in use" negative case from the bead.
    private static (HttpListener listener, int port) StartLoopbackListener()
    {
        Exception? last = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var port = GetFreePort();
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                listener.Start();
                return (listener, port);
            }
            catch (HttpListenerException ex)
            {
                last = ex;
                ((IDisposable)listener).Dispose();
            }
        }
        throw new OAuthException("Could not bind a loopback port for the OAuth redirect.", last!);
    }

    private static int GetFreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static string BuildAuthorizationUrl(
        string clientId, string redirectUri, IReadOnlyList<string> scopes, string state, string challenge)
    {
        var qs = new[]
        {
            ("client_id", clientId),
            ("redirect_uri", redirectUri),
            ("response_type", "code"),
            ("scope", string.Join(' ', scopes)),
            ("state", state),
            ("code_challenge", challenge),
            ("code_challenge_method", "S256"),
            ("access_type", "offline"),
            ("prompt", "consent"),
        };
        var encoded = string.Join("&",
            qs.Select(p => $"{Uri.EscapeDataString(p.Item1)}={Uri.EscapeDataString(p.Item2)}"));
        return $"{AuthEndpoint}?{encoded}";
    }

    private static (string? code, string? state, string? error) ParseCallback(string query)
    {
        if (query.StartsWith('?')) query = query[1..];

        string? code = null, state = null, error = null;
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = part.IndexOf('=');
            if (idx < 0) continue;
            var k = Uri.UnescapeDataString(part[..idx]);
            var v = Uri.UnescapeDataString(part[(idx + 1)..]);
            switch (k)
            {
                case "code": code = v; break;
                case "state": state = v; break;
                case "error": error = v; break;
            }
        }
        return (code, state, error);
    }

    private static async Task WriteResponseAsync(HttpListenerResponse resp, string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        resp.ContentType = "text/html; charset=utf-8";
        resp.StatusCode = 200;
        resp.ContentLength64 = bytes.Length;
        await resp.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        resp.OutputStream.Close();
    }

    private const string SuccessHtml =
        "<!doctype html><html><body style='font-family:system-ui;text-align:center;padding-top:6em'>"
        + "<h1>StreamManager — connected</h1><p>You can close this tab and return to the app.</p></body></html>";

    private const string FailHtml =
        "<!doctype html><html><body style='font-family:system-ui;text-align:center;padding-top:6em'>"
        + "<h1>StreamManager — authorization failed</h1><p>You can close this tab and try again from the app.</p></body></html>";

    private static string GenerateCodeVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));
    private static string GenerateRandomToken(int bytes) => Base64Url(RandomNumberGenerator.GetBytes(bytes));

    private static string ComputeS256Challenge(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int? ExpiresIn { get; set; }
        [JsonPropertyName("token_type")] public string? TokenType { get; set; }
        [JsonPropertyName("scope")] public string? Scope { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
        [JsonPropertyName("error_description")] public string? ErrorDescription { get; set; }
    }
}
