using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StreamManager.Core.Auth;

namespace StreamManager.App.ViewModels;

// First-run setup view (design.md §6.1 step 1). Two text fields plus a Save
// button — until the user fills both in and saves, Connect is unreachable
// from the rest of the UI. Acts as the gate from the `IsFirstRun` shell
// state into the main shell.
public sealed partial class FirstRunSetupViewModel : ObservableObject
{
    private readonly IOAuthClientConfig _config;
    private readonly ILogger<FirstRunSetupViewModel> _log;

    public FirstRunSetupViewModel(IOAuthClientConfig config, ILogger<FirstRunSetupViewModel> log)
    {
        _config = config;
        _log = log;
        var existing = _config.Current;
        _clientId = existing?.ClientId ?? string.Empty;
        _clientSecret = existing?.ClientSecret ?? string.Empty;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _clientId = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _clientSecret = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public event EventHandler? Saved;

    private bool CanSave =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken ct)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _config.SaveAsync(
                new OAuthClient(ClientId.Trim(), ClientSecret.Trim()), ct);
            _log.LogInformation("OAuth client configuration saved to disk");
            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to persist OAuth client configuration");
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
