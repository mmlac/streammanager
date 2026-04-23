using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace StreamManager.Core.Youtube;

public sealed class CategoryProvider : ICategoryProvider
{
    // Design slice 6: "Cache TTL: 30 days". Anything older triggers a
    // background refresh on next launch; cached data is still served
    // immediately so startup stays instant.
    internal static readonly TimeSpan CacheTtl = TimeSpan.FromDays(30);

    private readonly IAppPaths _paths;
    private readonly IYouTubeClient _client;
    private readonly TimeProvider _clock;
    private readonly ILogger<CategoryProvider> _log;
    private readonly object _lock = new();

    private string _regionCode = "US";
    private IReadOnlyList<VideoCategoryListItem> _current = Array.Empty<VideoCategoryListItem>();
    private bool _isLoading;
    private string? _lastErrorMessage;
    private Task _backgroundRefresh = Task.CompletedTask;

    public CategoryProvider(
        IAppPaths paths,
        IYouTubeClient client,
        TimeProvider clock,
        ILogger<CategoryProvider> log)
    {
        _paths = paths;
        _client = client;
        _clock = clock;
        _log = log;
    }

    public IReadOnlyList<VideoCategoryListItem> Current
    {
        get { lock (_lock) { return _current; } }
    }

    public string RegionCode
    {
        get { lock (_lock) { return _regionCode; } }
    }

    public bool IsLoading
    {
        get { lock (_lock) { return _isLoading; } }
    }

    public string? LastErrorMessage
    {
        get { lock (_lock) { return _lastErrorMessage; } }
    }

    public Task BackgroundRefreshTask
    {
        get { lock (_lock) { return _backgroundRefresh; } }
    }

    public event EventHandler? Changed;

    public async Task EnsureLoadedAsync(CancellationToken ct)
    {
        var region = RegionCode;
        var (entry, file) = ReadCache();

        if (entry is not null && TryMatchEntry(entry, region, out var items))
        {
            SetCurrent(items, errorMessage: null);
            if (IsStale(entry.RetrievedAt))
            {
                // Stale cache: fire-and-forget refresh so the cached data
                // shows instantly and the UI updates on the next tick.
                BeginBackgroundRefresh(region);
            }
            return;
        }

        // No usable entry → await a live fetch.
        await FetchAndWriteAsync(region, file, ct).ConfigureAwait(false);
    }

    public Task RefreshAsync(CancellationToken ct)
    {
        var region = RegionCode;
        var (_, file) = ReadCache();
        return FetchAndWriteAsync(region, file, ct);
    }

    public async Task SetRegionCodeAsync(string regionCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(regionCode))
        {
            throw new ArgumentException("regionCode is required.", nameof(regionCode));
        }

        lock (_lock)
        {
            if (string.Equals(_regionCode, regionCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            _regionCode = regionCode;
            _current = Array.Empty<VideoCategoryListItem>();
            _lastErrorMessage = null;
        }
        RaiseChanged();

        await EnsureLoadedAsync(ct).ConfigureAwait(false);
    }

    private async Task FetchAndWriteAsync(
        string region,
        CategoriesCacheFile file,
        CancellationToken ct)
    {
        SetLoading(true);
        try
        {
            var items = await _client.ListVideoCategoriesAsync(region, ct).ConfigureAwait(false);
            var entry = new CategoriesCacheEntry
            {
                RetrievedAt = _clock.GetUtcNow(),
                Items = items.ToList(),
            };
            file.Entries[region] = entry;
            WriteCache(file);
            SetCurrent(items, errorMessage: null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "videoCategories.list failed for region {Region}", region);
            // Preserve whatever Current held before — we only overwrite with
            // empty if this was the very first call and we have no cached
            // data (Current was already Array.Empty). The UI checks
            // LastErrorMessage + Count==0 to decide between "Loading…" and
            // the retry placeholder.
            SetError(ex.Message);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void BeginBackgroundRefresh(string region)
    {
        lock (_lock)
        {
            if (!_backgroundRefresh.IsCompleted)
            {
                return;
            }
            _backgroundRefresh = Task.Run(async () =>
            {
                try
                {
                    await RefreshAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Background category refresh failed");
                }
            });
        }
    }

    private (CategoriesCacheEntry? Entry, CategoriesCacheFile File) ReadCache()
    {
        var path = CachePath;
        var region = RegionCode;
        if (!File.Exists(path))
        {
            return (null, new CategoriesCacheFile());
        }

        try
        {
            var raw = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<CategoriesCacheFile>(
                raw, ReferenceCacheJson.Options);
            if (file is null)
            {
                return (null, new CategoriesCacheFile());
            }
            file.Entries ??= new Dictionary<string, CategoriesCacheEntry>(
                StringComparer.OrdinalIgnoreCase);
            file.Entries.TryGetValue(region, out var entry);
            return (entry, file);
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex, "categories.json is corrupt; treating as missing");
            return (null, new CategoriesCacheFile());
        }
        catch (IOException ex)
        {
            _log.LogWarning(ex, "Could not read categories.json");
            return (null, new CategoriesCacheFile());
        }
    }

    private void WriteCache(CategoriesCacheFile file)
    {
        _paths.EnsureDirectoriesExist();
        var path = CachePath;
        try
        {
            var json = JsonSerializer.Serialize(file, ReferenceCacheJson.Options);
            File.WriteAllText(path, json);
        }
        catch (IOException ex)
        {
            _log.LogWarning(ex, "Could not write categories.json to {Path}", path);
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.LogWarning(ex, "Permission denied writing categories.json to {Path}", path);
        }
    }

    private string CachePath => Path.Combine(_paths.CacheDirectory, "categories.json");

    private bool IsStale(DateTimeOffset retrievedAt) =>
        _clock.GetUtcNow() - retrievedAt > CacheTtl;

    private static bool TryMatchEntry(
        CategoriesCacheEntry entry,
        string region,
        out IReadOnlyList<VideoCategoryListItem> items)
    {
        items = (entry.Items ?? new List<VideoCategoryListItem>()).ToArray();
        return items.Count > 0;
    }

    private void SetCurrent(IReadOnlyList<VideoCategoryListItem> items, string? errorMessage)
    {
        lock (_lock)
        {
            _current = items;
            _lastErrorMessage = errorMessage;
        }
        RaiseChanged();
    }

    private void SetError(string message)
    {
        lock (_lock)
        {
            _lastErrorMessage = message;
        }
        RaiseChanged();
    }

    private void SetLoading(bool value)
    {
        lock (_lock)
        {
            if (_isLoading == value) return;
            _isLoading = value;
        }
        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
