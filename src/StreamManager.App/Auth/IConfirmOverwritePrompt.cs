namespace StreamManager.App.Auth;

// UX seam for the §6.2 "Overwrite unsaved changes?" dialog. Kept behind an
// interface so StreamFormDirtyFormGuard is unit-testable without spinning up
// an Avalonia dialog. The concrete implementation is UiConfirmOverwritePrompt.
public interface IConfirmOverwritePrompt
{
    Task<bool> ShowAsync(CancellationToken ct);
}
