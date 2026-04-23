using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using StreamManager.App.ViewModels;
using StreamManager.App.Views;
using StreamManager.Core.Auth;

namespace StreamManager.App.Auth;

// Avalonia-side adapter for IReauthPrompt. The orchestrator runs on whatever
// thread the failing API call returned on; we marshal back onto the UI thread
// to show a modal dialog parented to the main window, then return its
// boolean result back to the orchestrator.
public sealed class UiReauthPrompt : IReauthPrompt
{
    private readonly Lazy<IClassicDesktopStyleApplicationLifetime> _desktop;

    public UiReauthPrompt(Lazy<IClassicDesktopStyleApplicationLifetime> desktop)
    {
        _desktop = desktop;
    }

    public async Task<bool> PromptAsync(CancellationToken ct)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var vm = new ReauthModalViewModel();
            var window = new ReauthModalWindow(vm);

            var owner = _desktop.Value.MainWindow;
            if (owner is not null)
            {
                await window.ShowDialog(owner);
            }
            else
            {
                window.Show();
                var tcs = new TaskCompletionSource();
                window.Closed += (_, _) => tcs.TrySetResult();
                await tcs.Task;
            }

            return vm.Result;
        });
    }
}
