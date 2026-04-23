using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Threading;
using StreamManager.App.ViewModels;
using StreamManager.App.Views;

namespace StreamManager.App.Presets;

// Real Avalonia implementation of IPresetDialogs. Builds tiny ad-hoc
// confirm windows for yes/no prompts (they share the same shape) and
// spawns SavePresetDialogWindow for the name-entry case. Every method
// marshals onto the UI thread so the VM layer can stay threading-agnostic.
public sealed class AvaloniaPresetDialogs : IPresetDialogs
{
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;

    public AvaloniaPresetDialogs(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _desktop = desktop;
    }

    public async Task<bool> ConfirmLoadOverDirtyFormAsync(string presetName) =>
        await ShowYesNoAsync(
            "Load preset?",
            $"The form has unsaved changes. Loading \"{presetName}\" will replace them.",
            confirmText: "Load",
            cancelText: "Cancel");

    public async Task<bool> ConfirmReplacePresetAsync(string presetName) =>
        await ShowYesNoAsync(
            "Replace existing?",
            $"A preset named \"{presetName}\" already exists. Replace it with the current form values?",
            confirmText: "Replace",
            cancelText: "Cancel");

    public async Task<bool> ConfirmDeletePresetAsync(string presetName) =>
        await ShowYesNoAsync(
            "Delete preset?",
            $"Delete preset \"{presetName}\"? This cannot be undone.",
            confirmText: "Delete",
            cancelText: "Cancel");

    public async Task<string?> PromptSaveAsNameAsync(IReadOnlyList<string> existingNames)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var vm = new SavePresetDialogViewModel(existingNames);
            var window = new SavePresetDialogWindow(vm);
            await ShowDialogAsync(window);
            return vm.WasCancelled ? null : vm.Result;
        });
    }

    public async Task ShowPresetLoadErrorAsync(string message) =>
        await ShowInfoAsync("Presets unavailable", message);

    private async Task<bool> ShowYesNoAsync(string title, string message, string confirmText, string cancelText)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var tcs = new TaskCompletionSource<bool>();
            var window = BuildConfirmWindow(title, message, confirmText, cancelText, tcs);
            await ShowDialogAsync(window);
            return await tcs.Task;
        });
    }

    private async Task ShowInfoAsync(string title, string message)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var tcs = new TaskCompletionSource<bool>();
            var window = BuildConfirmWindow(title, message, confirmText: "OK", cancelText: null, tcs);
            await ShowDialogAsync(window);
            await tcs.Task;
        });
    }

    private async Task ShowDialogAsync(Window window)
    {
        var owner = _desktop.MainWindow;
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
    }

    private static Window BuildConfirmWindow(
        string title,
        string message,
        string confirmText,
        string? cancelText,
        TaskCompletionSource<bool> tcs)
    {
        var w = new Window
        {
            Title = title,
            Width = 380,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        var confirmBtn = new Button { Content = confirmText, Padding = new Avalonia.Thickness(12, 4) };
        confirmBtn.Classes.Add("accent");
        confirmBtn.Click += (_, _) => { tcs.TrySetResult(true); w.Close(); };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        if (cancelText is not null)
        {
            var cancelBtn = new Button { Content = cancelText, Padding = new Avalonia.Thickness(12, 4) };
            cancelBtn.Click += (_, _) => { tcs.TrySetResult(false); w.Close(); };
            buttons.Children.Add(cancelBtn);
        }

        buttons.Children.Add(confirmBtn);

        w.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = title, FontWeight = Avalonia.Media.FontWeight.SemiBold, FontSize = 16 },
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                buttons,
            },
        };

        // Close-button without hitting an action = cancel-equivalent. Use
        // TrySetResult so we don't overwrite a deliberate click.
        w.Closed += (_, _) => tcs.TrySetResult(false);
        return w;
    }
}
