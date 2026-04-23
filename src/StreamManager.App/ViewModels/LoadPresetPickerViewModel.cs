using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamManager.Core.Presets;

namespace StreamManager.App.ViewModels;

// Exposes the preset list for the Load ▾ dropdown (design §8) plus the
// per-row Load / Delete commands. Thin wrapper around the orchestrator —
// all persistence lives in PresetActionsViewModel; this VM's job is
// binding-friendly presentation and routing clicks to the orchestrator's
// callbacks.
public sealed partial class LoadPresetPickerViewModel : ObservableObject
{
    private readonly ObservableCollection<Preset> _presets = new();
    private readonly Func<Preset, Task> _onLoad;
    private readonly Func<Preset, Task> _onDelete;

    public LoadPresetPickerViewModel(
        Func<Preset, Task> onLoad,
        Func<Preset, Task> onDelete)
    {
        _onLoad = onLoad ?? throw new ArgumentNullException(nameof(onLoad));
        _onDelete = onDelete ?? throw new ArgumentNullException(nameof(onDelete));
        Presets = new ReadOnlyObservableCollection<Preset>(_presets);
    }

    public ReadOnlyObservableCollection<Preset> Presets { get; }

    public bool HasPresets => _presets.Count > 0;

    public void SetPresets(IEnumerable<Preset> presets)
    {
        ArgumentNullException.ThrowIfNull(presets);
        _presets.Clear();
        foreach (var p in presets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            _presets.Add(p);
        }
        OnPropertyChanged(nameof(HasPresets));
    }

    [RelayCommand]
    private async Task LoadAsync(Preset? preset)
    {
        if (preset is null) return;
        await _onLoad(preset).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteAsync(Preset? preset)
    {
        if (preset is null) return;
        await _onDelete(preset).ConfigureAwait(true);
    }
}
