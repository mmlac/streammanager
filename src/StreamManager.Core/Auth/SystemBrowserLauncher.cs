using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StreamManager.Core.Auth;

public sealed class SystemBrowserLauncher : IBrowserLauncher
{
    public void Launch(string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // UseShellExecute is required for `start` semantics on Windows.
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
            return;
        }

        Process.Start("xdg-open", url);
    }
}
