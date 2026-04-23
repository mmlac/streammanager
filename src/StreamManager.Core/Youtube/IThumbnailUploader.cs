namespace StreamManager.Core.Youtube;

// §6.6 step 4 — thumbnails.set. Lives behind its own interface so:
//   1. The Apply orchestrator's seam stays narrow (one method, no SDK types).
//   2. Tests can fake the upload without touching the Google.Apis SDK.
//
// Concrete impl: YouTubeThumbnailUploader.
public interface IThumbnailUploader
{
    Task SetThumbnailAsync(string videoId, string filePath, CancellationToken ct);
}
