using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Threading;

namespace StreamManager.App.Services;

// Avalonia modal that surfaces the §6.6 step 1 unreachable-thumbnail warning
// before the orchestrator decides whether to proceed. Built programmatically
// — a simple two-button dialog doesn't warrant its own XAML file (mirrors
// the existing UiConfirmOverwritePrompt).
public sealed class UiUnreachableThumbnailPrompt : IUnreachableThumbnailPrompt
{
    private readonly Lazy<IClassicDesktopStyleApplicationLifetime> _lifetime;

    public UiUnreachableThumbnailPrompt(Lazy<IClassicDesktopStyleApplicationLifetime> lifetime)
    {
        _lifetime = lifetime;
    }

    public Task<UnreachableThumbnailDecision> PromptAsync(string thumbnailPath, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<UnreachableThumbnailDecision>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = ct.Register(() => tcs.TrySetResult(UnreachableThumbnailDecision.Cancel));

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var result = await ShowDialogAsync(thumbnailPath).ConfigureAwait(true);
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private async Task<UnreachableThumbnailDecision> ShowDialogAsync(string thumbnailPath)
    {
        var owner = _lifetime.Value.MainWindow;
        if (owner is null)
        {
            // No main window means we can't show modal UI — safest is to
            // cancel rather than silently proceeding without the thumbnail.
            return UnreachableThumbnailDecision.Cancel;
        }

        var dialog = BuildDialog(thumbnailPath, out var decisionAccessor);
        await dialog.ShowDialog(owner).ConfigureAwait(true);
        return decisionAccessor();
    }

    private static Window BuildDialog(string thumbnailPath, out Func<UnreachableThumbnailDecision> decisionAccessor)
    {
        var decision = UnreachableThumbnailDecision.Cancel;
        var dialog = new Window
        {
            Title = "Thumbnail file not reachable",
            Width = 480,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.Height,
        };

        var message = new TextBlock
        {
            Text = $"Thumbnail file not reachable at:\n{thumbnailPath}\n\n" +
                   "Apply without updating the thumbnail, or cancel?",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(16),
        };

        var cancelButton = new Button { Content = "Cancel", MinWidth = 80 };
        var applyWithoutButton = new Button { Content = "Apply without thumbnail", MinWidth = 160 };
        cancelButton.Click += (_, _) => { decision = UnreachableThumbnailDecision.Cancel; dialog.Close(); };
        applyWithoutButton.Click += (_, _) => { decision = UnreachableThumbnailDecision.ApplyWithoutThumbnail; dialog.Close(); };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(16, 0, 16, 16),
            Spacing = 8,
        };
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(applyWithoutButton);

        var panel = new StackPanel();
        panel.Children.Add(message);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        decisionAccessor = () => decision;
        return dialog;
    }
}
