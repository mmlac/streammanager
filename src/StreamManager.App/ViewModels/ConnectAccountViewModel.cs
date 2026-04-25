using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StreamManager.Core.Auth;

namespace StreamManager.App.ViewModels;

// Top-bar Connect/Disconnect widget (design.md §8). Reflects IAuthState and
// drives IYouTubeAuthenticator. While the OAuth flow is in progress the
// commands disable themselves; if the user-provided OAuth client isn't
// configured yet, Connect stays disabled (the first-run setup screen will
// be shown by MainWindowViewModel anyway, but we belt-and-brace here).
public sealed partial class ConnectAccountViewModel : ObservableObject, IDisposable
{
    private readonly IYouTubeAuthenticator _authenticator;
    private readonly IAuthState _state;
    private readonly IOAuthClientConfig _config;
    private readonly ILogger<ConnectAccountViewModel> _log;

    public ConnectAccountViewModel(
        IYouTubeAuthenticator authenticator,
        IAuthState state,
        IOAuthClientConfig config,
        ILogger<ConnectAccountViewModel> log)
    {
        _authenticator = authenticator;
        _state = state;
        _config = config;
        _log = log;

        _state.Changed += OnStateChanged;
        _config.Changed += OnStateChanged;
        Refresh();
    }

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string? _accountEmail;

    [ObservableProperty]
    private string? _avatarUrl;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isClientConfigured;

    // Set while the browser-based OAuth flow is in progress so the user can
    // copy the URL manually if the browser failed to open.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingAuthUrl))]
    private string? _pendingAuthUrl;

    public bool HasPendingAuthUrl => PendingAuthUrl is not null;

    private bool CanConnect => !IsBusy && !IsConnected && IsClientConfigured;
    private bool CanDisconnect => !IsBusy && IsConnected;

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync(CancellationToken ct)
    {
        IsBusy = true;
        ErrorMessage = null;
        PendingAuthUrl = null;
        try
        {
            await _authenticator.ConnectInteractiveAsync(ct,
                onAuthUrlReady: url => Dispatcher.UIThread.Post(() => PendingAuthUrl = url));
        }
        catch (OperationCanceledException)
        {
            // User cancelled — not an error worth displaying.
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Interactive connect failed");
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            PendingAuthUrl = null;
        }
    }

    private bool CanCancelConnect => IsBusy;

    [RelayCommand(CanExecute = nameof(CanCancelConnect))]
    private void CancelConnect() => ConnectCommand.Cancel();

    [RelayCommand(CanExecute = nameof(HasPendingAuthUrl))]
    private async Task CopyAuthUrlAsync()
    {
        if (PendingAuthUrl is null) return;
        var clipboard = Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desk
            ? desk.MainWindow?.Clipboard
            : null;
        if (clipboard is not null)
            await clipboard.SetTextAsync(PendingAuthUrl);
    }

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync(CancellationToken ct)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _authenticator.DisconnectAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Disconnect failed");
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIsBusyChanged(bool value) => RaiseCanExecute();
    partial void OnIsConnectedChanged(bool value) => RaiseCanExecute();
    partial void OnIsClientConfiguredChanged(bool value) => RaiseCanExecute();
    partial void OnPendingAuthUrlChanged(string? value) => CopyAuthUrlCommand.NotifyCanExecuteChanged();

    private void RaiseCanExecute()
    {
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        CancelConnectCommand.NotifyCanExecuteChanged();
    }

    // AuthState.Changed fires on a thread-pool thread (the OAuth helpers use
    // ConfigureAwait(false) throughout).  Marshal back to the UI thread before
    // touching any observable properties.
    private void OnStateChanged(object? sender, EventArgs e)
        => Dispatcher.UIThread.Post(Refresh);

    private void Refresh()
    {
        IsConnected = _state.IsConnected;
        AccountEmail = _state.Account?.Email;
        AvatarUrl = _state.Account?.AvatarUrl;
        IsClientConfigured = _config.IsConfigured;
    }

    public void Dispose()
    {
        _state.Changed -= OnStateChanged;
        _config.Changed -= OnStateChanged;
    }
}
