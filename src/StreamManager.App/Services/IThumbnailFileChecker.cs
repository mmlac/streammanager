namespace StreamManager.App.Services;

// Indirection over File.Exists / readability so ApplyOrchestrator can be
// unit-tested against virtual paths without touching the filesystem. The
// production implementation is FileSystemThumbnailChecker.
public interface IThumbnailFileChecker
{
    bool IsReachable(string path);
}

public sealed class FileSystemThumbnailChecker : IThumbnailFileChecker
{
    public bool IsReachable(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        try
        {
            // §6.6 step 1: existence + readability. Opening the file with
            // FileShare.Read confirms permission-denied / locked cases that
            // File.Exists doesn't surface. Disposing immediately keeps the
            // check side-effect-free.
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
