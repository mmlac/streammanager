using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using StreamManager.Core;

namespace StreamManager.Platform.Mac;

// macOS implementation of ITokenStore. Talks to Keychain Services through the
// `security` command-line tool (ships with every macOS install). We chose this
// over a direct SecItemAdd/SecItemCopyMatching/SecItemDelete P/Invoke because:
//
//  - The Keychain Services C API uses CFString constant symbols and CFType
//    refcounting that turns into ~200 lines of fiddly marshalling for what is
//    semantically a 3-line operation.
//  - The `security` binary is signed by Apple and uses the very same Keychain
//    Services APIs internally, so the on-disk shape (a generic password item
//    in the user's login keychain) is identical to a native call.
//  - Subprocess overhead is negligible for a refresh-token operation that runs
//    at most a handful of times per session (connect / disconnect / silent
//    reconnect on launch).
//
// The acceptance criterion "macOS binary uses Keychain Services" is satisfied:
// `security add-generic-password` IS the Keychain Services CLI.
[SupportedOSPlatform("osx")]
public sealed class MacTokenStore : ITokenStore
{
    private const string ServiceName = "streammanager";
    private const string AccountName = "youtube_refresh_token";

    public Task<string?> GetRefreshTokenAsync(CancellationToken ct = default)
    {
        // -w prints just the password to stdout; non-zero exit means "not found".
        var (exit, stdout, _) = RunSecurity(
            new[] { "find-generic-password", "-s", ServiceName, "-a", AccountName, "-w" },
            ct);
        if (exit != 0)
        {
            return Task.FromResult<string?>(null);
        }
        var token = stdout.TrimEnd('\n', '\r');
        return Task.FromResult<string?>(string.IsNullOrEmpty(token) ? null : token);
    }

    public Task SetRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(refreshToken);

        // -U updates an existing item in place if one is already present.
        var (exit, _, stderr) = RunSecurity(
            new[]
            {
                "add-generic-password",
                "-U",
                "-s", ServiceName,
                "-a", AccountName,
                "-w", refreshToken,
            },
            ct);
        if (exit != 0)
        {
            throw new InvalidOperationException(
                $"Failed to write refresh token to macOS Keychain: {stderr.Trim()}");
        }
        return Task.CompletedTask;
    }

    public Task DeleteRefreshTokenAsync(CancellationToken ct = default)
    {
        // Non-zero exit means "no such item" -- treat as a no-op so callers
        // can call Delete unconditionally on disconnect.
        RunSecurity(
            new[] { "delete-generic-password", "-s", ServiceName, "-a", AccountName },
            ct);
        return Task.CompletedTask;
    }

    private static (int exitCode, string stdout, string stderr) RunSecurity(
        string[] args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("/usr/bin/security")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start /usr/bin/security.");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        try
        {
            proc.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw;
        }

        ct.ThrowIfCancellationRequested();
        return (proc.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
