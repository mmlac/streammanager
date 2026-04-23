namespace StreamManager.Core.Auth;

public interface IReauthOrchestrator
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        ReauthOperationOptions options,
        CancellationToken ct);

    Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        ReauthOperationOptions options,
        CancellationToken ct);
}

public sealed record ReauthOperationOptions
{
    public static readonly ReauthOperationOptions Default = new();

    // True when the operation is a re-fetch that would overwrite the form
    // (design.md §6.2). After a successful reauth the orchestrator runs
    // IDirtyFormGuard.ConfirmOverwriteAsync before retrying — reauth doesn't
    // bypass dirty-form protection (§6.7 step 3).
    public bool DirtyFormGuardOnRetry { get; init; }
}
