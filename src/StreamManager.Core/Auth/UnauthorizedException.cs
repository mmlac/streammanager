namespace StreamManager.Core.Auth;

// Thrown by any code path that observes a 401 from a YouTube/Google API call.
// Caught by IReauthOrchestrator to drive the reconnect modal + retry pipeline
// described in design.md §6.7.
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
    public UnauthorizedException(string message, Exception inner) : base(message, inner) { }
}
