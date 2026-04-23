namespace StreamManager.App.Services;

// Pushes the form's current values to the live broadcast (design.md §6.6).
// Steps 1..6 of the design land here; the thumbnails.set upload is routed
// through IThumbnailUploader (YouTubeThumbnailUploader in production).
public interface IApplyOrchestrator
{
    Task<ApplyResult> ApplyAsync(CancellationToken ct);
}
