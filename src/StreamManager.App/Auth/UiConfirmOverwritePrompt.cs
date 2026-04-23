using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Threading;

namespace StreamManager.App.Auth;

// Avalonia modal that asks "overwrite unsaved changes?" before a fetch
// clobbers the form (design.md §6.2). Built programmatically — a single
// yes/no prompt doesn't warrant its own XAML file.
public sealed class UiConfirmOverwritePrompt : IConfirmOverwritePrompt
{
    private readonly Lazy<IClassicDesktopStyleApplicationLifetime> _lifetime;

    public UiConfirmOverwritePrompt(Lazy<IClassicDesktopStyleApplicationLifetime> lifetime)
    {
        _lifetime = lifetime;
    }

    public Task<bool> ShowAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = ct.Register(() => tcs.TrySetResult(false));

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var result = await ShowDialogAsync().ConfigureAwait(true);
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private async Task<bool> ShowDialogAsync()
    {
        var owner = _lifetime.Value.MainWindow;
        if (owner is null)
        {
            // Main window not up yet (earliest startup). Without an owner we
            // can't show a modal — fall through to "cancel" so we don't
            // silently clobber the form.
            return false;
        }

        var dialog = BuildDialog(out var confirmed);
        await dialog.ShowDialog(owner).ConfigureAwait(true);
        return confirmed();
    }

    private static Window BuildDialog(out Func<bool> confirmedAccessor)
    {
        var confirmed = false;
        var dialog = new Window
        {
            Title = "Overwrite unsaved changes?",
            Width = 420,
            Height = 160,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.Height,
        };

        var message = new TextBlock
        {
            Text = "The stream form has unsaved edits. Refreshing from YouTube will replace them. Continue?",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(16),
        };

        var cancelButton = new Button { Content = "Cancel", MinWidth = 80 };
        var overwriteButton = new Button { Content = "Overwrite", MinWidth = 80 };
        cancelButton.Click += (_, _) => { confirmed = false; dialog.Close(); };
        overwriteButton.Click += (_, _) => { confirmed = true; dialog.Close(); };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(16, 0, 16, 16),
            Spacing = 8,
        };
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(overwriteButton);

        var panel = new StackPanel();
        panel.Children.Add(message);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        confirmedAccessor = () => confirmed;
        return dialog;
    }
}
