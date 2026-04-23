namespace StreamManager.App.Services;

// Pushes the form's current values to the live broadcast (design.md §6.6).
// Steps 1..6 of the design land here; the actual thumbnails.set upload is
// stubbed via IThumbnailUploader until slice 8 wires the real call.
public interface IApplyOrchestrator
{
    Task<ApplyResult> ApplyAsync(CancellationToken ct);
}
