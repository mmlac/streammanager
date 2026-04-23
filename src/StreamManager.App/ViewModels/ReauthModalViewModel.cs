using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StreamManager.App.ViewModels;

// Backs ReauthModalWindow. The orchestrator awaits a Task<bool>; the window
// reads `Result` once it closes. Result is `true` if the user accepted, `false`
// otherwise (Cancel button or window close).
public sealed partial class ReauthModalViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _result;

    public event EventHandler<bool>? Closed;

    [RelayCommand]
    private void Reconnect()
    {
        Result = true;
        Closed?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        Result = false;
        Closed?.Invoke(this, false);
    }
}
