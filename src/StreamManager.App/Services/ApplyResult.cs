namespace StreamManager.App.Services;

// Which §6.6 step is currently running / which one failed. Exposed by the
// orchestrator's progress + error surface so the UI can label the spinner
// ("Updating broadcast…", "Updating video…", "Uploading thumbnail…") and
// the error card ("Step 1 failed: …").
public enum ApplyStep
{
    None,
    PreflightThumbnail,
    UpdateBroadcast,
    UpdateVideo,
    UpdateThumbnail,
    RefetchAfterApply,
}

public enum ApplyOutcome
{
    Success,
    Cancelled,
    Failed,
}

// Return value from IApplyOrchestrator.ApplyAsync. Mirrors StreamFetchResult
// in shape so the MainWindowViewModel can react uniformly. FailedStep is set
// when Outcome == Failed; ErrorMessage is set on Failed AND on Cancelled
// (e.g. "cancelled by user" so the bottom-bar status line has something to
// say after the dialog dismisses).
public sealed record ApplyResult(
    ApplyOutcome Outcome,
    ApplyStep FailedStep = ApplyStep.None,
    string? ErrorMessage = null,
    bool BroadcastUpdated = false);
