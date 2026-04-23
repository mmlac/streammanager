using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StreamManager.App.Services;

namespace StreamManager.App.ViewModels;

// §6.8 + slice-8 picker. Exposes Pick / Clear commands on the form's
// ThumbnailPath and a preview-state surface (local path when reachable,
// "unreachable" placeholder data when the file is missing / on a detached
// drive). Change tracking still lives on StreamFormViewModel.
// IsThumbnailChangedFromLive — this VM only mutates ThumbnailPath.
public sealed partial class ThumbnailPickerViewModel : ObservableObject, IDisposable
{
    private readonly StreamFormViewModel _form;
    private readonly IThumbnailPickerService _picker;
    private readonly IThumbnailFileValidator _validator;
    private readonly IThumbnailValidationPrompt _prompt;
    private readonly IThumbnailFileChecker _checker;
    private readonly ILogger<ThumbnailPickerViewModel> _log;

    public ThumbnailPickerViewModel(
        StreamFormViewModel form,
        IThumbnailPickerService picker,
        IThumbnailFileValidator validator,
        IThumbnailValidationPrompt prompt,
        IThumbnailFileChecker checker,
        ILogger<ThumbnailPickerViewModel> log)
    {
        _form = form;
        _picker = picker;
        _validator = validator;
        _prompt = prompt;
        _checker = checker;
        _log = log;

        _form.PropertyChanged += OnFormPropertyChanged;
    }

    public string? LocalPreviewPath =>
        !string.IsNullOrEmpty(_form.ThumbnailPath) && _checker.IsReachable(_form.ThumbnailPath)
            ? _form.ThumbnailPath
            : null;

    public bool HasLocalPreview => LocalPreviewPath is not null;

    public bool ShowUnreachablePlaceholder =>
        !string.IsNullOrEmpty(_form.ThumbnailPath) && !_checker.IsReachable(_form.ThumbnailPath);

    // Same string as ThumbnailPath when unreachable — surfaced verbatim in the
    // placeholder card so the user knows which drive to reconnect.
    public string? UnreachablePathText =>
        ShowUnreachablePlaceholder ? _form.ThumbnailPath : null;

    public bool HasThumbnailPath => !string.IsNullOrEmpty(_form.ThumbnailPath);

    [RelayCommand]
    private async Task PickAsync(CancellationToken ct)
    {
        string? path;
        try
        {
            path = await _picker.PickFileAsync(ct).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Thumbnail picker threw");
            return;
        }
        if (string.IsNullOrEmpty(path))
        {
            // User cancelled — no state change.
            return;
        }

        var issue = _validator.Validate(path);
        if (issue != ThumbnailValidationIssue.Ok)
        {
            _log.LogInformation(
                "Thumbnail picked but rejected: {Issue} ({Path})", issue, path);
            await _prompt.ShowAsync(issue, path, ct).ConfigureAwait(true);
            // State unchanged — explicit acceptance criterion.
            return;
        }

        // Accepted. Setting ThumbnailPath fires PropertyChanged on the form,
        // which our handler below will translate into preview-state updates.
        _form.ThumbnailPath = path;
    }

    [RelayCommand]
    private void Clear()
    {
        _form.ThumbnailPath = null;
    }

    // Recompute preview properties whenever the form's ThumbnailPath changes
    // (covers Pick, Clear, preset load, and fetch-baselining).
    private void OnFormPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StreamFormViewModel.ThumbnailPath))
        {
            OnPropertyChanged(nameof(LocalPreviewPath));
            OnPropertyChanged(nameof(HasLocalPreview));
            OnPropertyChanged(nameof(ShowUnreachablePlaceholder));
            OnPropertyChanged(nameof(UnreachablePathText));
            OnPropertyChanged(nameof(HasThumbnailPath));
        }
    }

    public void Dispose()
    {
        _form.PropertyChanged -= OnFormPropertyChanged;
    }
}
