namespace StreamManager.Core.Auth;

public sealed class OAuthException : Exception
{
    public OAuthException(string message) : base(message) { }
    public OAuthException(string message, Exception inner) : base(message, inner) { }
}
