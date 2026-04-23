using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using StreamManager.Core;

namespace StreamManager.Platform.Windows;

// Windows implementation of ITokenStore. Stores the refresh token as a
// CRED_TYPE_GENERIC entry in the user's Windows Credential Manager via the
// advapi32 CredRead/CredWrite/CredDelete P/Invoke surface.
//
// The refresh token is held by `CRED_PERSIST_LOCAL_MACHINE` so it survives a
// reboot but never leaves this machine (we don't want it roaming via a domain
// profile to a host the user didn't intend).
[SupportedOSPlatform("windows")]
public sealed class WindowsTokenStore : ITokenStore
{
    private const string TargetName = "streammanager:youtube_refresh_token";
    private const int ErrorNotFound = 1168;

    public Task<string?> GetRefreshTokenAsync(CancellationToken ct = default)
    {
        if (!CredRead(TargetName, CredentialType.Generic, 0, out var credPtr))
        {
            var err = Marshal.GetLastWin32Error();
            if (err == ErrorNotFound)
            {
                return Task.FromResult<string?>(null);
            }
            throw new Win32Exception(err, "CredRead failed");
        }

        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            if (cred.CredentialBlob == IntPtr.Zero || cred.CredentialBlobSize == 0)
            {
                return Task.FromResult<string?>(null);
            }
            var bytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, cred.CredentialBlobSize);
            return Task.FromResult<string?>(Encoding.Unicode.GetString(bytes));
        }
        finally
        {
            CredFree(credPtr);
        }
    }

    public Task SetRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(refreshToken);

        var bytes = Encoding.Unicode.GetBytes(refreshToken);
        var blob = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var cred = new CREDENTIAL
            {
                Type = CredentialType.Generic,
                TargetName = TargetName,
                CredentialBlobSize = bytes.Length,
                CredentialBlob = blob,
                Persist = CredentialPersist.LocalMachine,
                UserName = "youtube",
            };

            if (!CredWrite(ref cred, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CredWrite failed");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blob);
        }
        return Task.CompletedTask;
    }

    public Task DeleteRefreshTokenAsync(CancellationToken ct = default)
    {
        if (!CredDelete(TargetName, CredentialType.Generic, 0))
        {
            var err = Marshal.GetLastWin32Error();
            // Treat "not found" as success so callers can call Delete
            // unconditionally on disconnect.
            if (err != ErrorNotFound)
            {
                throw new Win32Exception(err, "CredDelete failed");
            }
        }
        return Task.CompletedTask;
    }

    private enum CredentialType : uint
    {
        Generic = 1,
    }

    private enum CredentialPersist : uint
    {
        Session = 1,
        LocalMachine = 2,
        Enterprise = 3,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public CredentialType Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public CredentialPersist Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, CredentialType type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, CredentialType type, int flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr cred);
}
