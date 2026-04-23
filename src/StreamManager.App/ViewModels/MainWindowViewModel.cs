using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StreamManager.Core.Auth;

namespace StreamManager.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IOAuthClientConfig _config;
    private readonly IYouTubeAuthenticator _authenticator;
    private readonly IServiceProvider _services;
    private readonly ILogger<MainWindowViewModel> _log;
    private readonly CancellationTokenSource _disposed = new();

    public MainWindowViewModel(
        IOAuthClientConfig config,
        IYouTubeAuthenticator authenticator,
        ConnectAccountViewModel connectAccount,
        StreamFormViewModel streamForm,
        IServiceProvider services,
        ILogger<MainWindowViewModel> log)
    {
        _config = config;
        _authenticator = authenticator;
        _services = services;
        _log = log;
        ConnectAccount = connectAccount;
        StreamForm = streamForm;

        _config.Changed += OnConfigChanged;
        Refresh();

        // Fire-and-forget silent reconnect on launch (design.md §6.1: refresh
        // token survives restart). Errors are logged inside the authenticator.
        _ = Task.Run(async () =>
        {
            try { await _authenticator.TrySilentReconnectAsync(_disposed.Token); }
            catch (OperationCanceledException) { /* shutdown */ }
            catch (Exception ex) { _log.LogWarning(ex, "Silent reconnect threw"); }
        });
    }

    public ConnectAccountViewModel ConnectAccount { get; }

    public StreamFormViewModel StreamForm { get; }

    [ObservableProperty]
    private bool _isFirstRun;

    [ObservableProperty]
    private FirstRunSetupViewModel? _firstRunViewModel;

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

    public void Dispose()
    {
        _disposed.Cancel();
        _disposed.Dispose();
        _config.Changed -= OnConfigChanged;
        if (FirstRunViewModel is not null)
        {
            FirstRunViewModel.Saved -= OnFirstRunSaved;
        }
        ConnectAccount.Dispose();
    }
}
