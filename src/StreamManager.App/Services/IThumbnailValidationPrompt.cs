namespace StreamManager.App.Services;

// Surfaces the §6.8 pick-time validation error to the user as a modal dialog.
// Abstracted so the picker VM stays threading-agnostic and unit-testable.
public interface IThumbnailValidationPrompt
{
    Task ShowAsync(ThumbnailValidationIssue issue, string path, CancellationToken ct);
}
