namespace StreamManager.Core.Youtube;

// Populates the Category dropdown from YouTube's `videoCategories.list`.
// Cached on disk at <AppData>/streammanager/cache/categories.json keyed
// by regionCode; stale-while-revalidate so startup never blocks on the
// network. See design §6.8 / slice 6.
public interface ICategoryProvider
{
    // Categories for the current regionCode. Empty until EnsureLoadedAsync
    // completes or a cached entry is read. Callers should re-read on
    // Changed to pick up background refresh + region-switch updates.
    IReadOnlyList<VideoCategoryListItem> Current { get; }

    string RegionCode { get; }

    // True while the first/refresh API call is in flight. Drives the
    // "Loading…" placeholder.
    bool IsLoading { get; }

    // Populated when the last refresh failed. Null when the cache is
    // serving data successfully. Drives the "retry" placeholder when
    // no cache exists (acceptance: "network failure when no cache…").
    string? LastErrorMessage { get; }

    event EventHandler? Changed;

    // Loads from cache if present; fetches from API only when cache is
    // missing. A stale cache (older than the TTL) triggers a background
    // refresh but returns immediately with the cached data. Tests can
    // await `BackgroundRefreshTask` to observe that refresh.
    Task EnsureLoadedAsync(CancellationToken ct);

    // Forces an API fetch and overwrites the cache for the current
    // regionCode, even if the existing entry is fresh. Used by the
    // Settings "Refresh" button.
    Task RefreshAsync(CancellationToken ct);

    // Switches regionCode. Each region has its own cache entry in
    // categories.json; the previous region's entry is preserved.
    Task SetRegionCodeAsync(string regionCode, CancellationToken ct);

    // Tests: the in-flight background refresh task, if any. Returns a
    // completed task when no refresh is running. Never null.
    Task BackgroundRefreshTask { get; }
}
