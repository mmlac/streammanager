using StreamManager.Core.Youtube;

namespace StreamManager.Core.Tests.Youtube;

// Test double for the slice 6 reference-data calls. The broadcast
// surface from slice 4 isn't exercised by the provider tests, so
// it throws.
internal sealed class FakeYouTubeReferenceClient : IYouTubeClient
{
    public Dictionary<string, IReadOnlyList<VideoCategoryListItem>> CategoriesByRegion { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<I18nLanguageListItem> Languages { get; set; } =
        Array.Empty<I18nLanguageListItem>();

    public Exception? ThrowOnCategories { get; set; }
    public Exception? ThrowOnLanguages { get; set; }

    public int CategoryCallCount { get; private set; }
    public int LanguageCallCount { get; private set; }

    public List<string> CategoryRegionsCalled { get; } = new();

    // Tests can set this to gate API calls on a signal — lets stale-cache
    // tests observe the cached snapshot before the background refresh
    // returns. When null, calls resolve synchronously.
    public TaskCompletionSource? CategoryGate { get; set; }
    public TaskCompletionSource? LanguageGate { get; set; }

    public Task<BroadcastSnapshot?> GetActiveBroadcastAsync(CancellationToken ct) =>
        throw new NotImplementedException();

    public Task UpdateBroadcastAsync(BroadcastUpdate update, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task UpdateVideoAsync(VideoUpdate update, CancellationToken ct) =>
        throw new NotImplementedException();

    public async Task<IReadOnlyList<VideoCategoryListItem>> ListVideoCategoriesAsync(
        string regionCode, CancellationToken ct)
    {
        CategoryCallCount++;
        CategoryRegionsCalled.Add(regionCode);
        if (CategoryGate is not null)
        {
            await CategoryGate.Task.ConfigureAwait(false);
        }
        if (ThrowOnCategories is not null) throw ThrowOnCategories;
        return CategoriesByRegion.TryGetValue(regionCode, out var items)
            ? items
            : Array.Empty<VideoCategoryListItem>();
    }

    public async Task<IReadOnlyList<I18nLanguageListItem>> ListI18nLanguagesAsync(
        CancellationToken ct)
    {
        LanguageCallCount++;
        if (LanguageGate is not null)
        {
            await LanguageGate.Task.ConfigureAwait(false);
        }
        if (ThrowOnLanguages is not null) throw ThrowOnLanguages;
        return Languages;
    }
}
