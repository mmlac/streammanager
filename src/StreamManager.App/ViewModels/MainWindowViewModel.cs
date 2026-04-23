using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StreamManager.App.Services;
using StreamManager.Core.Auth;

namespace StreamManager.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IOAuthClientConfig _config;
    private readonly IYouTubeAuthenticator _authenticator;
    private readonly IAuthState _authState;
    private readonly IStreamFetchCoordinator _fetchCoordinator;
    private readonly IServiceProvider _services;
    private readonly ILogger<MainWindowViewModel> _log;
    private readonly CancellationTokenSource _disposed = new();

    public MainWindowViewModel(
        IOAuthClientConfig config,
        IYouTubeAuthenticator authenticator,
        IAuthState authState,
        IStreamFetchCoordinator fetchCoordinator,
        ConnectAccountViewModel connectAccount,
        StreamFormViewModel streamForm,
        PresetActionsViewModel presetActions,
        IServiceProvider services,
        ILogger<MainWindowViewModel> log)
    {
        _config = config;
        _authenticator = authenticator;
        _authState = authState;
        _fetchCoordinator = fetchCoordinator;
        _services = services;
        _log = log;
        ConnectAccount = connectAccount;
        StreamForm = streamForm;
        PresetActions = presetActions;

        _config.Changed += OnConfigChanged;
        _authState.Changed += OnAuthStateChanged;
        SyncFromAuthState();
        Refresh();

        // Fire-and-forget silent reconnect on launch (design.md §6.1: refresh
        // token survives restart). Errors are logged inside the authenticator.
        // Once a successful reconnect flips IsConnected, the auth-state handler
        // below triggers the startup fetch (§6.2 "app startup triggers one
        // fetch automatically after connect").
        _ = Task.Run(async () =>
        {
            try { await _authenticator.TrySilentReconnectAsync(_disposed.Token); }
            catch (OperationCanceledException) { /* shutdown */ }
            catch (Exception ex) { _log.LogWarning(ex, "Silent reconnect threw"); }
        });
    }

    public ConnectAccountViewModel ConnectAccount { get; }

    public StreamFormViewModel StreamForm { get; }

    public PresetActionsViewModel PresetActions { get; }

    [ObservableProperty]
    private bool _isFirstRun;

    [ObservableProperty]
    private FirstRunSetupViewModel? _firstRunViewModel;

    [ObservableProperty]
    private LiveIndicatorStatus _liveIndicator = LiveIndicatorStatus.Unknown;

    [ObservableProperty]
    private string? _fetchErrorMessage;

    [ObservableProperty]
    private bool _isFetching;

    public bool CanRefresh => _authState.IsConnected && !IsFetching;

    // Derived indicator flags consumed by MainWindow.axaml for the top-bar dot.
    public bool IsLiveIndicatorLive => LiveIndicator == LiveIndicatorStatus.Live;
    public bool IsLiveIndicatorNotLive => LiveIndicator == LiveIndicatorStatus.NotLive;
    public bool IsLiveIndicatorFailed => LiveIndicator == LiveIndicatorStatus.FetchFailed;

    public string LiveIndicatorText => LiveIndicator switch
    {
        LiveIndicatorStatus.Live => "Live",
        LiveIndicatorStatus.NotLive => "Not live",
        LiveIndicatorStatus.FetchFailed => "Fetch failed",
        _ => "—",
    };

    partial void OnLiveIndicatorChanged(LiveIndicatorStatus value)
    {
        OnPropertyChanged(nameof(IsLiveIndicatorLive));
        OnPropertyChanged(nameof(IsLiveIndicatorNotLive));
        OnPropertyChanged(nameof(IsLiveIndicatorFailed));
        OnPropertyChanged(nameof(LiveIndicatorText));
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync(CancellationToken ct)
    {
        await RunFetchAsync(allowOverwrite: false, ct).ConfigureAwait(false);
    }

    // Slice 5 will call this after a successful Apply so the dirty-form
    // confirmation is skipped (the user just pushed the edits).
    public Task<StreamFetchResult> FetchAfterApplyAsync(CancellationToken ct) =>
        RunFetchAsync(allowOverwrite: true, ct);

    private async Task<StreamFetchResult> RunFetchAsync(bool allowOverwrite, CancellationToken ct)
    {
        if (!_authState.IsConnected)
        {
            var notConnected = new StreamFetchResult(
                StreamFetchOutcome.FetchFailed,
                LiveIndicatorStatus.Unknown,
                ErrorMessage: "Not connected.");
            ApplyResultToIndicator(notConnected);
            return notConnected;
        }

        IsFetching = true;
        FetchErrorMessage = null;
        try
        {
            var result = await _fetchCoordinator.FetchAsync(allowOverwrite, ct).ConfigureAwait(false);
            ApplyResultToIndicator(result);
            return result;
        }
        finally
        {
            IsFetching = false;
        }
    }

    private void ApplyResultToIndicator(StreamFetchResult result)
    {
        // CancelledByDirtyGuard leaves the indicator untouched (user just
        // bailed out; no new information about the live state).
        if (result.Outcome == StreamFetchOutcome.Cancelled)
        {
            return;
        }
        LiveIndicator = result.Status;
        FetchErrorMessage = result.ErrorMessage;
    }

    partial void OnIsFetchingChanged(bool value) =>
        RefreshCommand.NotifyCanExecuteChanged();

    private void Refresh()
    {
        var firstRun = !_config.IsConfigured;
        IsFirstRun = firstRun;
        if (firstRun)
        {
            if (FirstRunViewModel is null)
            {
                var vm = _services.GetRequiredService<FirstRunSetupViewModel>();
                vm.Saved += OnFirstRunSaved;
                FirstRunViewModel = vm;
            }
        }
        else if (FirstRunViewModel is not null)
        {
            FirstRunViewModel.Saved -= OnFirstRunSaved;
            FirstRunViewModel = null;
        }
    }

    private void OnFirstRunSaved(object? sender, EventArgs e) => Refresh();

    private void OnConfigChanged(object? sender, EventArgs e) => Refresh();

    private void OnAuthStateChanged(object? sender, EventArgs e)
    {
        SyncFromAuthState();
        RefreshCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRefresh));

        // §6.2: "App startup triggers one fetch automatically (after connect)."
        // Every Connected transition triggers a fresh fetch — covers launch
        // (after silent reconnect), interactive Connect, and reconnect after
        // Disconnect so the form always mirrors the newly-active account.
        if (_authState.IsConnected)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await RunFetchAsync(allowOverwrite: false, _disposed.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) { /* shutdown */ }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Post-connect fetch failed");
                }
            });
        }
        else
        {
            // Disconnected: reset indicator so UI doesn't linger on green.
            LiveIndicator = LiveIndicatorStatus.Unknown;
            StreamForm.HasLiveBroadcast = false;
            StreamForm.RemoteThumbnailUrl = null;
        }
    }

    private void SyncFromAuthState()
    {
        StreamForm.IsConnected = _authState.IsConnected;
    }

    public void Dispose()
    {
        _disposed.Cancel();
        _disposed.Dispose();
        _config.Changed -= OnConfigChanged;
        _authState.Changed -= OnAuthStateChanged;
        if (FirstRunViewModel is not null)
        {
            FirstRunViewModel.Saved -= OnFirstRunSaved;
        }
        ConnectAccount.Dispose();
    }
}
