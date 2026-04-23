using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StreamManager.App.Presets;
using StreamManager.Core.Presets;

namespace StreamManager.App.ViewModels;

// Single owner of the preset lifecycle (§6.3–§6.5): loads the file on
// startup, routes Load / Save-as / Update / Delete through the dialogs,
// and coordinates form lineage updates. Everything a button in the §8
// preset action bar can trigger is exposed here as a command.
public sealed partial class PresetActionsViewModel : ObservableObject
{
    private readonly IPresetStore _store;
    private readonly IPresetDialogs _dialogs;
    private readonly StreamFormViewModel _form;
    private readonly TimeProvider _time;
    private readonly ILogger<PresetActionsViewModel> _log;

    private PresetsFile _currentFile = PresetsFile.Empty;
    private bool _storeIsHealthy = true;

    public PresetActionsViewModel(
        IPresetStore store,
        IPresetDialogs dialogs,
        StreamFormViewModel form,
        TimeProvider time,
        ILogger<PresetActionsViewModel> log)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _form = form ?? throw new ArgumentNullException(nameof(form));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        LoadPicker = new LoadPresetPickerViewModel(LoadPresetAsync, DeletePresetAsync);
        _form.PropertyChanged += OnFormPropertyChanged;
        _presets = new ObservableCollection<Preset>();
        Presets = new ReadOnlyObservableCollection<Preset>(_presets);

        InitializeFromStore();
    }

    private readonly ObservableCollection<Preset> _presets;

    public LoadPresetPickerViewModel LoadPicker { get; }

    public ReadOnlyObservableCollection<Preset> Presets { get; }

    public bool HasPresets => _presets.Count > 0;

    public bool StoreIsHealthy => _storeIsHealthy;

    // Save-as is available whenever the form validates; it doesn't depend
    // on lineage. Disabled when the store is unreadable (fatal schema
    // error), otherwise the user could save and then get a mismatched
    // on-disk state.
    public bool CanSaveAs => _storeIsHealthy && !_form.HasErrors;

    public bool CanUpdate => _storeIsHealthy && _form.CanUpdatePreset;

    public bool ShowUpdateButton => _storeIsHealthy && _form.HasPresetLineage;

    public string UpdateButtonText =>
        _form.PresetLineage is null
            ? "Update preset"
            : $"Update preset \"{_form.PresetLineage.Name}\"";

    private void InitializeFromStore()
    {
        try
        {
            _currentFile = _store.Load();
            _storeIsHealthy = true;
            SyncPresetsCollection();
        }
        catch (PresetStoreException ex)
        {
            _log.LogError(ex,
                "Could not load presets.json (code {Code}): {Message}",
                ex.Code, ex.Message);
            _storeIsHealthy = false;
            _ = _dialogs.ShowPresetLoadErrorAsync(FormatLoadError(ex));
            NotifyCanExecuteChanged();
        }
    }

    private static string FormatLoadError(PresetStoreException ex) => ex.Code switch
    {
        PresetStoreErrorCode.NewerThanSupported =>
            "This presets.json was written by a newer version of StreamManager. Please update.",
        PresetStoreErrorCode.UnknownOlder =>
            "This presets.json uses an older schema that this build cannot read.",
        PresetStoreErrorCode.Malformed =>
            "presets.json is malformed and could not be loaded.",
        _ => "Could not load presets.json: " + ex.Message,
    };

    [RelayCommand(CanExecute = nameof(CanSaveAs))]
    private async Task SaveAsAsync()
    {
        if (!CanSaveAs) return;

        var existingNames = _currentFile.Presets.Select(p => p.Name).ToArray();
        var chosen = await _dialogs.PromptSaveAsNameAsync(existingNames).ConfigureAwait(true);
        if (chosen is null) return;

        var trimmed = chosen.Trim();
        if (trimmed.Length == 0) return;

        var match = _currentFile.Presets.FirstOrDefault(
            p => string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            var replace = await _dialogs.ConfirmReplacePresetAsync(match.Name).ConfigureAwait(true);
            if (!replace) return;

            var now = _time.GetUtcNow();
            var replacement = PresetMapper.ToPreset(
                _form.CaptureSnapshot(),
                id: match.Id,
                name: match.Name,
                createdAt: match.CreatedAt,
                updatedAt: now);
            ReplacePresetInStore(replacement);
            _form.SetPresetBaselineFromCurrent(new PresetLineage(replacement.Id, replacement.Name));
            return;
        }

        var created = PresetMapper.ToPreset(
            _form.CaptureSnapshot(),
            id: Guid.NewGuid().ToString("D"),
            name: trimmed,
            createdAt: _time.GetUtcNow(),
            updatedAt: _time.GetUtcNow());

        AppendPresetInStore(created);
        _form.SetPresetBaselineFromCurrent(new PresetLineage(created.Id, created.Name));
    }

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private void UpdateCurrent()
    {
        if (!CanUpdate) return;
        var lineage = _form.PresetLineage!;
        var existing = _currentFile.Presets.FirstOrDefault(p => p.Id == lineage.Id);
        if (existing is null)
        {
            // Lineage points to a preset no longer in the store (raced with
            // delete from another window). Clear lineage so the UI matches
            // reality; surfacing a toast is out of scope for v1.
            _form.ClearPresetLineage();
            return;
        }

        var updated = PresetMapper.ToPreset(
            _form.CaptureSnapshot(),
            id: existing.Id,
            name: existing.Name,
            createdAt: existing.CreatedAt,
            updatedAt: _time.GetUtcNow());
        ReplacePresetInStore(updated);
        _form.RebaselineCurrentPreset();
    }

    private async Task LoadPresetAsync(Preset preset)
    {
        if (!_storeIsHealthy) return;

        if (_form.IsDirtyVsLive || _form.IsDirtyVsPreset)
        {
            var proceed = await _dialogs.ConfirmLoadOverDirtyFormAsync(preset.Name).ConfigureAwait(true);
            if (!proceed) return;
        }

        _form.SetPresetBaseline(
            PresetMapper.ToSnapshot(preset),
            new PresetLineage(preset.Id, preset.Name));
    }

    private async Task DeletePresetAsync(Preset preset)
    {
        if (!_storeIsHealthy) return;

        var proceed = await _dialogs.ConfirmDeletePresetAsync(preset.Name).ConfigureAwait(true);
        if (!proceed) return;

        var remaining = _currentFile.Presets.Where(p => p.Id != preset.Id).ToArray();
        var updated = new PresetsFile(PresetStore.CurrentSchemaVersion, remaining);
        _store.Save(updated);
        _currentFile = updated;
        SyncPresetsCollection();

        if (_form.PresetLineage?.Id == preset.Id)
        {
            _form.ClearPresetLineage();
        }
    }

    private void AppendPresetInStore(Preset preset)
    {
        var next = _currentFile.Presets.Concat(new[] { preset }).ToArray();
        var file = new PresetsFile(PresetStore.CurrentSchemaVersion, next);
        _store.Save(file);
        _currentFile = file;
        SyncPresetsCollection();
    }

    private void ReplacePresetInStore(Preset preset)
    {
        var next = _currentFile.Presets
            .Select(p => p.Id == preset.Id ? preset : p)
            .ToArray();
        var file = new PresetsFile(PresetStore.CurrentSchemaVersion, next);
        _store.Save(file);
        _currentFile = file;
        SyncPresetsCollection();
    }

    private void SyncPresetsCollection()
    {
        _presets.Clear();
        foreach (var p in _currentFile.Presets) _presets.Add(p);
        LoadPicker.SetPresets(_currentFile.Presets);
        OnPropertyChanged(nameof(HasPresets));
        NotifyCanExecuteChanged();
    }

    private void OnFormPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is
            nameof(StreamFormViewModel.HasPresetLineage) or
            nameof(StreamFormViewModel.CanUpdatePreset) or
            nameof(StreamFormViewModel.PresetLineage) or
            nameof(StreamFormViewModel.HasErrors))
        {
            OnPropertyChanged(nameof(CanSaveAs));
            OnPropertyChanged(nameof(CanUpdate));
            OnPropertyChanged(nameof(ShowUpdateButton));
            OnPropertyChanged(nameof(UpdateButtonText));
            NotifyCanExecuteChanged();
        }
    }

    private void NotifyCanExecuteChanged()
    {
        SaveAsCommand.NotifyCanExecuteChanged();
        UpdateCurrentCommand.NotifyCanExecuteChanged();
    }
}
