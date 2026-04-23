namespace StreamManager.Core.Auth;

// Surface the §6.2 confirm-before-overwrite dialog. Returns true if the user
// agreed to overwrite (or the form is clean), false to abort the overwrite.
//
// The guard is invoked by IReauthOrchestrator after a successful reauth when
// the original operation would clobber a dirty form (a re-fetch). Passing the
// callback in via DI (rather than baking it into the orchestrator) keeps the
// Core layer free of any UI dependency.
public interface IDirtyFormGuard
{
    Task<bool> ConfirmOverwriteAsync(CancellationToken ct);
}
