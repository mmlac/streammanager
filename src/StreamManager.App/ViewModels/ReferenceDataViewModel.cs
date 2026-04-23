using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StreamManager.Core;
using StreamManager.Core.Auth;
using StreamManager.Core.Youtube;

namespace StreamManager.App.ViewModels;

// Feeds the Category + Language ComboBoxes (slice 6). The form still
// owns the selected value (`CategoryId` / `DefaultLanguage` / etc.) on
// StreamFormViewModel — this VM owns the *options list* and the
// loading/error state so those concerns stay out of the form.
public sealed partial class ReferenceDataViewModel : ObservableObject, IDisposable
{
    private readonly ICategoryProvider _categories;
    private readonly ILanguageProvider _languages;
    private readonly IAppSettings _appSettings;
    private readonly IAuthState _authState;
    private readonly StreamFormViewModel _streamForm;
    private readonly ILogger<ReferenceDataViewModel> _log;
    private readonly CancellationTokenSource _disposed = new();
    private bool _isDisposed;

    public ReferenceDataViewModel(
        ICategoryProvider categories,
        ILanguageProvider languages,
        IAppSettings appSettings,
        IAuthState authState,
        StreamFormViewModel streamForm,
        ILogger<ReferenceDataViewModel> log)
    {
        _categories = categories;
        _languages = languages;
        _appSettings = appSettings;
        _authState = authState;
        _streamForm = streamForm;
        _log = log;

        _regionCode = _appSettings.RegionCode;

        _categories.Changed += OnCategoriesChanged;
        _languages.Changed += OnLanguagesChanged;
        _appSettings.Changed += OnAppSettingsChanged;
        _authState.Changed += OnAuthStateChanged;
        _streamForm.PropertyChanged += OnStreamFormChanged;

        SyncCategories();
        SyncLanguages();
        SyncSelectedCategory();
        SyncSelectedLanguages();
    }

    public ObservableCollection<CategoryDropdownItem> CategoryOptions { get; } = new();
    public ObservableCollection<LanguageDropdownItem> LanguageOptions { get; } = new();

    // AutoCompleteBox needs SelectedItem binding (no SelectedValue/Path
    // support). These proxy the selection to/from the form's CategoryId
    // / DefaultLanguage / DefaultAudioLanguage — dropdown is the UI,
    // the form is still the single source of truth on Apply.
    private CategoryDropdownItem? _selectedCategory;
    public CategoryDropdownItem? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetProperty(ref _selectedCategory, value)) return;
            _streamForm.CategoryId = value?.Id;
        }
    }

    private LanguageDropdownItem? _selectedLanguage;
    public LanguageDropdownItem? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!SetProperty(ref _selectedLanguage, value)) return;
            _streamForm.DefaultLanguage = value?.Hl;
        }
    }

    private LanguageDropdownItem? _selectedAudioLanguage;
    public LanguageDropdownItem? SelectedAudioLanguage
    {
        get => _selectedAudioLanguage;
        set
        {
            if (!SetProperty(ref _selectedAudioLanguage, value)) return;
            _streamForm.DefaultAudioLanguage = value?.Hl;
        }
    }

    [ObservableProperty]
    private bool _isCategoriesLoading;

    [ObservableProperty]
    private string? _categoriesErrorMessage;

    [ObservableProperty]
    private bool _isLanguagesLoading;

    [ObservableProperty]
    private string? _languagesErrorMessage;

    [ObservableProperty]
    private string _regionCode;

    public bool HasCategoryOptions => CategoryOptions.Count > 0;
    public bool HasLanguageOptions => LanguageOptions.Count > 0;

    // Drives the "Loading…" vs "retry" placeholder. Loading shown while
    // a fetch is in flight; retry shown when no cache and the last fetch
    // failed (§acceptance: "fall back to free-text so Apply isn't blocked").
    public bool ShowCategoriesLoadingPlaceholder =>
        !HasCategoryOptions && IsCategoriesLoading;
    public bool ShowCategoriesRetryPlaceholder =>
        !HasCategoryOptions && !IsCategoriesLoading && CategoriesErrorMessage is not null;

    public bool ShowLanguagesLoadingPlaceholder =>
        !HasLanguageOptions && IsLanguagesLoading;
    public bool ShowLanguagesRetryPlaceholder =>
        !HasLanguageOptions && !IsLanguagesLoading && LanguagesErrorMessage is not null;

    [RelayCommand]
    private Task RefreshCategoriesAsync() => TryRefreshCategoriesAsync();

    [RelayCommand]
    private Task RefreshLanguagesAsync() => TryRefreshLanguagesAsync();

    // Called by MainWindowViewModel once the auth state is known (startup
    // or after a Connect). Safe to call any time — if not connected, we
    // still try to serve from cache.
    public async Task EnsureLoadedAsync(CancellationToken ct)
    {
        try
        {
            await _categories.EnsureLoadedAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Category ensure-load failed");
        }

        try
        {
            await _languages.EnsureLoadedAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Language ensure-load failed");
        }
    }

    partial void OnRegionCodeChanged(string value)
    {
        if (!string.Equals(_appSettings.RegionCode, value, StringComparison.OrdinalIgnoreCase))
        {
            // Persist + trigger a fetch of the new region's categories.
            _ = Task.Run(async () =>
            {
                try
                {
                    await _appSettings.SetRegionCodeAsync(value, _disposed.Token)
                        .ConfigureAwait(false);
                    await _categories.SetRegionCodeAsync(value, _disposed.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) { /* shutdown */ }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Region code change to {Region} failed", value);
                }
            });
        }
    }

    private async Task TryRefreshCategoriesAsync()
    {
        try
        {
            await _categories.RefreshAsync(_disposed.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Manual category refresh failed");
        }
    }

    private async Task TryRefreshLanguagesAsync()
    {
        try
        {
            await _languages.RefreshAsync(_disposed.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Manual language refresh failed");
        }
    }

    private void OnCategoriesChanged(object? sender, EventArgs e) =>
        PostToUi(SyncCategories);

    private void OnLanguagesChanged(object? sender, EventArgs e) =>
        PostToUi(SyncLanguages);

    private void OnAppSettingsChanged(object? sender, EventArgs e) =>
        PostToUi(() => RegionCode = _appSettings.RegionCode);

    private void OnAuthStateChanged(object? sender, EventArgs e)
    {
        if (_authState.IsConnected)
        {
            // Newly connected → pull reference data. Covers the first-run
            // case where the form is shown with empty dropdowns and no cache.
            _ = Task.Run(() => EnsureLoadedAsync(_disposed.Token));
        }
    }

    private void SyncCategories()
    {
        var items = _categories.Current;
        CategoryOptions.Clear();
        foreach (var item in items)
        {
            if (item.Snippet is null || item.Snippet.Assignable == false)
            {
                // Filter out non-assignable categories — YouTube flags
                // some legacy/deprecated entries as assignable:false and
                // rejects them on videos.update.
                continue;
            }
            CategoryOptions.Add(new CategoryDropdownItem(
                item.Id,
                item.Snippet?.Title ?? item.Id));
        }
        IsCategoriesLoading = _categories.IsLoading;
        CategoriesErrorMessage = _categories.LastErrorMessage;
        OnPropertyChanged(nameof(HasCategoryOptions));
        OnPropertyChanged(nameof(ShowCategoriesLoadingPlaceholder));
        OnPropertyChanged(nameof(ShowCategoriesRetryPlaceholder));
        SyncSelectedCategory();
    }

    private void SyncLanguages()
    {
        var items = _languages.Current;
        LanguageOptions.Clear();
        foreach (var item in items)
        {
            var hl = item.Snippet?.Hl ?? item.Id;
            var name = item.Snippet?.Name ?? item.Id;
            LanguageOptions.Add(new LanguageDropdownItem(hl, name));
        }
        IsLanguagesLoading = _languages.IsLoading;
        LanguagesErrorMessage = _languages.LastErrorMessage;
        OnPropertyChanged(nameof(HasLanguageOptions));
        OnPropertyChanged(nameof(ShowLanguagesLoadingPlaceholder));
        OnPropertyChanged(nameof(ShowLanguagesRetryPlaceholder));
        SyncSelectedLanguages();
    }

    private void SyncSelectedCategory()
    {
        var id = _streamForm.CategoryId;
        var match = id is null
            ? null
            : CategoryOptions.FirstOrDefault(c =>
                string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        if (!ReferenceEquals(_selectedCategory, match))
        {
            _selectedCategory = match;
            OnPropertyChanged(nameof(SelectedCategory));
        }
    }

    private void SyncSelectedLanguages()
    {
        var defaultLang = _streamForm.DefaultLanguage;
        var matchDefault = defaultLang is null
            ? null
            : LanguageOptions.FirstOrDefault(l =>
                string.Equals(l.Hl, defaultLang, StringComparison.OrdinalIgnoreCase));
        if (!ReferenceEquals(_selectedLanguage, matchDefault))
        {
            _selectedLanguage = matchDefault;
            OnPropertyChanged(nameof(SelectedLanguage));
        }

        var audioLang = _streamForm.DefaultAudioLanguage;
        var matchAudio = audioLang is null
            ? null
            : LanguageOptions.FirstOrDefault(l =>
                string.Equals(l.Hl, audioLang, StringComparison.OrdinalIgnoreCase));
        if (!ReferenceEquals(_selectedAudioLanguage, matchAudio))
        {
            _selectedAudioLanguage = matchAudio;
            OnPropertyChanged(nameof(SelectedAudioLanguage));
        }
    }

    private void OnStreamFormChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StreamFormViewModel.CategoryId))
        {
            PostToUi(SyncSelectedCategory);
        }
        else if (e.PropertyName == nameof(StreamFormViewModel.DefaultLanguage) ||
                 e.PropertyName == nameof(StreamFormViewModel.DefaultAudioLanguage))
        {
            PostToUi(SyncSelectedLanguages);
        }
    }

    private static void PostToUi(Action action)
    {
        var dispatcher = Dispatcher.UIThread;
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Post(action);
        }
    }

    partial void OnIsCategoriesLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCategoriesLoadingPlaceholder));
        OnPropertyChanged(nameof(ShowCategoriesRetryPlaceholder));
    }

    partial void OnCategoriesErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowCategoriesRetryPlaceholder));
    }

    partial void OnIsLanguagesLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowLanguagesLoadingPlaceholder));
        OnPropertyChanged(nameof(ShowLanguagesRetryPlaceholder));
    }

    partial void OnLanguagesErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowLanguagesRetryPlaceholder));
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _disposed.Cancel();
        _disposed.Dispose();
        _categories.Changed -= OnCategoriesChanged;
        _languages.Changed -= OnLanguagesChanged;
        _appSettings.Changed -= OnAppSettingsChanged;
        _authState.Changed -= OnAuthStateChanged;
        _streamForm.PropertyChanged -= OnStreamFormChanged;
    }
}

public sealed record CategoryDropdownItem(string Id, string Title)
{
    public override string ToString() => Title;
}

public sealed record LanguageDropdownItem(string Hl, string Name)
{
    // Shown in the ComboBox. "en — English" reads better than just "en";
    // the combined form also lets Avalonia's searchable ComboBox match
    // either the code or the name.
    public string DisplayText => string.IsNullOrEmpty(Name)
        ? Hl
        : $"{Hl} — {Name}";

    public override string ToString() => DisplayText;
}
