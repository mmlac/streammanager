using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace StreamManager.Core.Youtube;

public sealed class LanguageProvider : ILanguageProvider
{
    internal static readonly TimeSpan CacheTtl = TimeSpan.FromDays(30);

    private readonly IAppPaths _paths;
    private readonly IYouTubeClient _client;
    private readonly TimeProvider _clock;
    private readonly ILogger<LanguageProvider> _log;
    private readonly object _lock = new();

    private IReadOnlyList<I18nLanguageListItem> _current = Array.Empty<I18nLanguageListItem>();
    private bool _isLoading;
    private string? _lastErrorMessage;
    private Task _backgroundRefresh = Task.CompletedTask;

    public LanguageProvider(
        IAppPaths paths,
        IYouTubeClient client,
        TimeProvider clock,
        ILogger<LanguageProvider> log)
    {
        _paths = paths;
        _client = client;
        _clock = clock;
        _log = log;
    }

    public IReadOnlyList<I18nLanguageListItem> Current
    {
        get { lock (_lock) { return _current; } }
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
        var file = ReadCache();

        if (file is not null && file.Items.Count > 0)
        {
            SetCurrent(file.Items.ToArray(), errorMessage: null);
            if (IsStale(file.RetrievedAt))
            {
                BeginBackgroundRefresh();
            }
            return;
        }

        await FetchAndWriteAsync(ct).ConfigureAwait(false);
    }

    public Task RefreshAsync(CancellationToken ct) => FetchAndWriteAsync(ct);

    private async Task FetchAndWriteAsync(CancellationToken ct)
    {
        SetLoading(true);
        try
        {
            var items = await _client.ListI18nLanguagesAsync(ct).ConfigureAwait(false);
            var file = new LanguagesCacheFile
            {
                RetrievedAt = _clock.GetUtcNow(),
                Items = items.ToList(),
            };
            WriteCache(file);
            SetCurrent(items, errorMessage: null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "i18nLanguages.list failed");
            SetError(ex.Message);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void BeginBackgroundRefresh()
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
                    _log.LogWarning(ex, "Background language refresh failed");
                }
            });
        }
    }

    private LanguagesCacheFile? ReadCache()
    {
        var path = CachePath;
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var raw = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LanguagesCacheFile>(
                raw, ReferenceCacheJson.Options);
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex, "languages.json is corrupt; treating as missing");
            return null;
        }
        catch (IOException ex)
        {
            _log.LogWarning(ex, "Could not read languages.json");
            return null;
        }
    }

    private void WriteCache(LanguagesCacheFile file)
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
            _log.LogWarning(ex, "Could not write languages.json to {Path}", path);
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.LogWarning(ex, "Permission denied writing languages.json to {Path}", path);
        }
    }

    private string CachePath => Path.Combine(_paths.CacheDirectory, "languages.json");

    private bool IsStale(DateTimeOffset retrievedAt) =>
        _clock.GetUtcNow() - retrievedAt > CacheTtl;

    private void SetCurrent(IReadOnlyList<I18nLanguageListItem> items, string? errorMessage)
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
