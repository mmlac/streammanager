using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Threading;

namespace StreamManager.App.Services;

// Avalonia modal for the §6.8 pick-time validation error. Mirrors the
// shape of UiUnreachableThumbnailPrompt (built programmatically; one button).
public sealed class UiThumbnailValidationPrompt : IThumbnailValidationPrompt
{
    private readonly Lazy<IClassicDesktopStyleApplicationLifetime> _lifetime;

    public UiThumbnailValidationPrompt(Lazy<IClassicDesktopStyleApplicationLifetime> lifetime)
    {
        _lifetime = lifetime;
    }

    public Task ShowAsync(ThumbnailValidationIssue issue, string path, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = ct.Register(() => tcs.TrySetResult(null));

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await ShowDialogAsync(issue, path).ConfigureAwait(true);
                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private async Task ShowDialogAsync(ThumbnailValidationIssue issue, string path)
    {
        var owner = _lifetime.Value.MainWindow;
        if (owner is null) return;

        var dialog = BuildDialog(issue, path);
        await dialog.ShowDialog(owner).ConfigureAwait(true);
    }

    private static Window BuildDialog(ThumbnailValidationIssue issue, string path)
    {
        var dialog = new Window
        {
            Title = "Thumbnail not accepted",
            Width = 480,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.Height,
        };

        var message = new TextBlock
        {
            Text = MessageFor(issue, path),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(16),
        };

        var ok = new Button { Content = "OK", MinWidth = 80 };
        ok.Click += (_, _) => dialog.Close();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(16, 0, 16, 16),
            Spacing = 8,
        };
        buttons.Children.Add(ok);

        var panel = new StackPanel();
        panel.Children.Add(message);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        return dialog;
    }

    internal static string MessageFor(ThumbnailValidationIssue issue, string path) => issue switch
    {
        ThumbnailValidationIssue.BadExtension =>
            $"The selected file is not a supported format:\n{path}\n\n" +
            "Thumbnails must be JPG, PNG, BMP, or GIF.",
        ThumbnailValidationIssue.TooLarge =>
            $"The selected file is larger than 2 MB:\n{path}\n\n" +
            "YouTube rejects thumbnails above 2 MB.",
        ThumbnailValidationIssue.Unreadable =>
            $"The selected file could not be read:\n{path}\n\n" +
            "Check that it exists and the app has permission to open it.",
        _ => $"The selected file is not acceptable:\n{path}",
    };
}
