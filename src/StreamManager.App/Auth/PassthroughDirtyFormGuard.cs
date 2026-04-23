using StreamManager.Core.Auth;

namespace StreamManager.App.Auth;

// Slice 2 placeholder. The form itself lands in slice 3, which will replace
// this with a guard that inspects StreamFormViewModel.IsDirty and shows the
// §6.2 confirm dialog. Until then there's nothing to overwrite, so we always
// proceed.
public sealed class PassthroughDirtyFormGuard : IDirtyFormGuard
{
    public Task<bool> ConfirmOverwriteAsync(CancellationToken ct) => Task.FromResult(true);
}
