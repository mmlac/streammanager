namespace StreamManager.Core.Youtube;

// Thin wrapper around Google.Apis.YouTube.v3 for the read/write surface
// described in design.md §4. Slice 4 added GetActiveBroadcastAsync; slice 5
// adds the Apply pipeline (UpdateBroadcastAsync + UpdateVideoAsync). The
// thumbnails.set call lives behind IThumbnailUploader so slice 8 can swap
// in a real implementation without touching the broadcast/video calls.
//
// 401 responses are translated to Core's UnauthorizedException so the
// IReauthOrchestrator pipeline can drive the §6.7 reconnect modal.
public interface IYouTubeClient
{
    Task<BroadcastSnapshot?> GetActiveBroadcastAsync(CancellationToken ct);

    // §6.6 step 2 — push snippet + status + contentDetails to the live broadcast.
    Task UpdateBroadcastAsync(BroadcastUpdate update, CancellationToken ct);

    // §6.6 step 3 — push videos.update fields (categoryId, tags, languages).
    Task UpdateVideoAsync(VideoUpdate update, CancellationToken ct);

    // Reference-data fetches for the Category + Language dropdowns (design
    // §4 / slice 6). These are public data but the app's OAuth client does
    // not carry an API key, so we sign the request with the active access
    // token like every other call; providers only invoke them when the
    // account is connected.
    Task<IReadOnlyList<VideoCategoryListItem>> ListVideoCategoriesAsync(
        string regionCode,
        CancellationToken ct);

    Task<IReadOnlyList<I18nLanguageListItem>> ListI18nLanguagesAsync(
        CancellationToken ct);
}
