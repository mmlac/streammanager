namespace StreamManager.App.Services;

// User decision when the form references a thumbnail file we can't reach
// at Apply time (design.md §6.6 step 1). The reference itself is never
// auto-cleared — the file may live on a detached drive the user will
// reconnect — so the choice is between proceeding without the thumbnail
// upload or aborting the whole Apply.
public enum UnreachableThumbnailDecision
{
    ApplyWithoutThumbnail,
    Cancel,
}

// UX seam for the §6.6 step 1 warning dialog. Kept behind an interface so
// the orchestrator is unit-testable without spinning up an Avalonia dialog.
// The concrete implementation is UiUnreachableThumbnailPrompt.
public interface IUnreachableThumbnailPrompt
{
    Task<UnreachableThumbnailDecision> PromptAsync(string thumbnailPath, CancellationToken ct);
}
