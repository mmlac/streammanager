namespace StreamManager.App.Services;

// Pick-time validation for the §6.8 thumbnail picker. Size ≤ 2 MB, format in
// {jpg, jpeg, png, bmp, gif}. Unreadable files (permission denied, race with
// deletion) surface as Unreadable so the VM can show the same "file not
// reachable" surface it uses at Apply time.
public enum ThumbnailValidationIssue
{
    Ok,
    BadExtension,
    TooLarge,
    Unreadable,
}

public interface IThumbnailFileValidator
{
    ThumbnailValidationIssue Validate(string path);
}

public sealed class FileSystemThumbnailValidator : IThumbnailFileValidator
{
    // YouTube's ceiling per design §6.8 / slice-8 acceptance.
    internal const long MaxSizeBytes = 2L * 1024 * 1024;

    internal static readonly string[] AllowedExtensions =
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

    public ThumbnailValidationIssue Validate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ThumbnailValidationIssue.Unreadable;
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (Array.IndexOf(AllowedExtensions, ext) < 0)
        {
            return ThumbnailValidationIssue.BadExtension;
        }

        long length;
        try
        {
            // FileInfo.Length opens the directory entry, not the file, so it
            // doesn't lock the file. Throws FileNotFoundException on missing.
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return ThumbnailValidationIssue.Unreadable;
            }
            length = info.Length;
        }
        catch (Exception)
        {
            return ThumbnailValidationIssue.Unreadable;
        }

        return length > MaxSizeBytes
            ? ThumbnailValidationIssue.TooLarge
            : ThumbnailValidationIssue.Ok;
    }
}
