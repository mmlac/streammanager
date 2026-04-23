using StreamManager.Core.Youtube;

namespace StreamManager.App.Services;

public enum StreamFetchOutcome
{
    Live,
    NotLive,
    FetchFailed,
    Cancelled,
}

// Return value from IStreamFetchCoordinator.FetchAsync. Carries both the new
// live-indicator status and a user-presentable error message for the amber
// banner case. The snapshot is returned too so slice 5 (post-Apply re-fetch)
// can reuse it for diff/verification.
public sealed record StreamFetchResult(
    StreamFetchOutcome Outcome,
    LiveIndicatorStatus Status,
    BroadcastSnapshot? Snapshot = null,
    string? ErrorMessage = null);
