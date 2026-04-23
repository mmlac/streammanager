namespace StreamManager.App.Services;

// Orchestrates the §6.2 "fetch current stream into form" flow: dirty-form
// guard → liveBroadcasts.list (+videos.list) via IYouTubeClient → form
// baseline update → live-indicator status.
//
// allowOverwrite skips the dirty-form confirmation (used by the post-Apply
// re-fetch in slice 5 where the user just pushed the edits).
public interface IStreamFetchCoordinator
{
    Task<StreamFetchResult> FetchAsync(bool allowOverwrite, CancellationToken ct);
}
