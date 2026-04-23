using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace StreamManager.App.Services;

// Real Avalonia-backed picker. Opens an OpenFilePicker dialog filtered to the
// JPG/PNG/BMP/GIF set called out in §6.8. Marshals onto the UI thread so the
// VM layer can stay threading-agnostic (mirrors AvaloniaPresetDialogs).
public sealed class AvaloniaThumbnailPickerService : IThumbnailPickerService
{
    private readonly Lazy<IClassicDesktopStyleApplicationLifetime> _lifetime;

    public AvaloniaThumbnailPickerService(Lazy<IClassicDesktopStyleApplicationLifetime> lifetime)
    {
        _lifetime = lifetime;
    }

    public Task<string?> PickFileAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = ct.Register(() => tcs.TrySetResult(null));

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var result = await PickAsyncCore().ConfigureAwait(true);
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private async Task<string?> PickAsyncCore()
    {
        var owner = _lifetime.Value.MainWindow;
        if (owner is null)
        {
            return null;
        }

        var fileTypes = new FilePickerFileType("Thumbnail images")
        {
            Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif" },
            MimeTypes = new[] { "image/jpeg", "image/png", "image/bmp", "image/gif" },
        };

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Pick a thumbnail image",
            AllowMultiple = false,
            FileTypeFilter = new[] { fileTypes },
        }).ConfigureAwait(true);

        if (files is null || files.Count == 0)
        {
            return null;
        }

        var file = files[0];
        // Avalonia's TryGetLocalPath returns the filesystem path for
        // file:// URIs; non-local items (remote storage) return null and we
        // reject them — the thumbnail uploader needs a real path.
        return file.TryGetLocalPath();
    }
}
