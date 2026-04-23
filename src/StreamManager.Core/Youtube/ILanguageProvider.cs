namespace StreamManager.Core.Youtube;

// Populates the language dropdowns (defaultLanguage, defaultAudioLanguage)
// from YouTube's `i18nLanguages.list`. Cached on disk at
// <AppData>/streammanager/cache/languages.json. See design §6.8 / slice 6.
public interface ILanguageProvider
{
    IReadOnlyList<I18nLanguageListItem> Current { get; }

    bool IsLoading { get; }

    string? LastErrorMessage { get; }

    event EventHandler? Changed;

    Task EnsureLoadedAsync(CancellationToken ct);

    Task RefreshAsync(CancellationToken ct);

    // Tests: the in-flight background refresh task, if any. Returns a
    // completed task when no refresh is running. Never null.
    Task BackgroundRefreshTask { get; }
}
