using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamManager.Core.Auth;

public sealed class UserInfoClient : IUserInfoClient
{
    private const string Endpoint = "https://www.googleapis.com/oauth2/v3/userinfo";

    private readonly HttpClient _http;

    public UserInfoClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<AccountInfo> FetchAsync(string accessToken, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(accessToken);

        using var req = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if ((int)resp.StatusCode == 401)
        {
            throw new UnauthorizedException("userinfo returned 401");
        }
        resp.EnsureSuccessStatusCode();

        var payload = await resp.Content.ReadFromJsonAsync<UserInfoResponse>(cancellationToken: ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Empty userinfo response.");
        return new AccountInfo(payload.Email ?? string.Empty, payload.Picture);
    }

    private sealed class UserInfoResponse
    {
        [JsonPropertyName("email")] public string? Email { get; set; }
        [JsonPropertyName("picture")] public string? Picture { get; set; }
    }
}
