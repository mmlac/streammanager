namespace StreamManager.Core.Auth;

public sealed record TokenSet(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc);
