namespace StreamManager.Core.Youtube;

// §6.6 step 4 — thumbnails.set. Lives behind its own interface so:
//   1. The Apply orchestrator's seam stays narrow (one method, no SDK types).
//   2. Slice 8 can replace this stub with a real Google.Apis call without
//      touching IYouTubeClient or any of its callers.
//
// The slice-5 implementation throws to make sure no production code path
// quietly hits it — the only legitimate caller (the orchestrator, step 4)
// is guarded by a "thumbnail changed AND reachable" check, and there is no
// thumbnail picker in slice 5 to set ThumbnailPath in the first place.
public interface IThumbnailUploader
{
    Task SetThumbnailAsync(string videoId, string filePath, CancellationToken ct);
}

// Slice-5 stub: the seam exists for the orchestrator and tests, but the
// real Google.Apis upload lands in slice 8. Throwing (rather than no-op)
// guarantees we notice if production wiring tries to use it before then.
public sealed class StubThumbnailUploader : IThumbnailUploader
{
    public Task SetThumbnailAsync(string videoId, string filePath, CancellationToken ct) =>
        throw new NotImplementedException(
            "thumbnails.set is wired in slice 8; slice 5 only defines the seam.");
}
