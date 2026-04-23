using Microsoft.Extensions.Logging;

namespace StreamManager.Core.Auth;

public sealed class ReauthOrchestrator : IReauthOrchestrator
{
    private readonly IYouTubeAuthenticator _authenticator;
    private readonly IReauthPrompt _prompt;
    private readonly IDirtyFormGuard _dirtyFormGuard;
    private readonly ILogger<ReauthOrchestrator> _log;

    public ReauthOrchestrator(
        IYouTubeAuthenticator authenticator,
        IReauthPrompt prompt,
        IDirtyFormGuard dirtyFormGuard,
        ILogger<ReauthOrchestrator> log)
    {
        _authenticator = authenticator;
        _prompt = prompt;
        _dirtyFormGuard = dirtyFormGuard;
        _log = log;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        ReauthOperationOptions options,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            return await operation(ct).ConfigureAwait(false);
        }
        catch (UnauthorizedException ex)
        {
            _log.LogInformation(ex, "401 from YouTube; prompting reauth");

            var accepted = await _prompt.PromptAsync(ct).ConfigureAwait(false);
            if (!accepted)
            {
                throw new OperationCanceledException("Reauth cancelled by user.", ex);
            }

            await _authenticator.ConnectInteractiveAsync(ct).ConfigureAwait(false);

            // §6.7 step 3: the reauth itself doesn't bypass the dirty-form
            // protection from §6.2. If the original call is a re-fetch that
            // would overwrite the form, ask the user before proceeding.
            if (options.DirtyFormGuardOnRetry)
            {
                var ok = await _dirtyFormGuard.ConfirmOverwriteAsync(ct).ConfigureAwait(false);
                if (!ok)
                {
                    throw new OperationCanceledException(
                        "Re-fetch aborted to preserve dirty form.");
                }
            }

            return await operation(ct).ConfigureAwait(false);
        }
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        ReauthOperationOptions options,
        CancellationToken ct)
    {
        await ExecuteAsync<object?>(async token =>
        {
            await operation(token).ConfigureAwait(false);
            return null;
        }, options, ct).ConfigureAwait(false);
    }
}
