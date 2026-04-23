namespace StreamManager.App.Services;

// File-dialog seam for the §6.8 thumbnail picker. Returns the absolute path
// the user selected, or null if they cancelled. Pick-time validation
// (size / format) lives in IThumbnailFileValidator so the picker service can
// stay a thin wrapper around the storage provider.
public interface IThumbnailPickerService
{
    Task<string?> PickFileAsync(CancellationToken ct);
}
