namespace StreamManager.Core.Auth;

public sealed record OAuthClient(string ClientId, string ClientSecret)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
