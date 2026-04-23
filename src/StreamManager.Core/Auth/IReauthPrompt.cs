namespace StreamManager.Core.Auth;

// Surface the reconnect modal described in design.md §6.7. Returns true if
// the user accepted (Connect button), false on Cancel/dismiss.
public interface IReauthPrompt
{
    Task<bool> PromptAsync(CancellationToken ct);
}
