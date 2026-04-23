using Microsoft.Extensions.Logging.Abstractions;
using StreamManager.App.ViewModels;
using StreamManager.Core;
using StreamManager.Core.Presets;
using Xunit;

namespace StreamManager.Core.Tests.Presets;

// Integration-ish: the orchestrator plus a real PresetStore backed by a
// tmp AppData root, plus the fake dialogs. Confirms the §6 flows end to
// end and that the on-disk presets.json actually matches the form state
// we expect after each command.
public class PresetActionsViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly AppPaths _paths;
    private readonly PresetStore _store;
    private readonly FakePresetDialogs _dialogs;
    private readonly FixedTimeProvider _time;
    private readonly StreamFormViewModel _form;
    private readonly PresetActionsViewModel _vm;

    public PresetActionsViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"sm-preset-vm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _paths = new AppPaths(_root);
        _store = new PresetStore(_paths);
        _dialogs = new FakePresetDialogs();
        _time = new FixedTimeProvider(DateTimeOffset.Parse("2026-04-23T18:00:00Z"));
        _form = new StreamFormViewModel();
        _vm = new PresetActionsViewModel(_store, _dialogs, _form, _time, NullLogger<PresetActionsViewModel>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            try { Directory.Delete(_root, recursive: true); }
            catch { }
        }
    }

    [Fact]
    public void FreshLaunch_NoPresetsJson_PickerIsEmpty_AndUpdateHidden()
    {
        Assert.False(_vm.HasPresets);
        Assert.False(_vm.LoadPicker.HasPresets);
        Assert.False(_vm.ShowUpdateButton);
        Assert.Equal("Update preset", _vm.UpdateButtonText);
        Assert.True(_vm.CanSaveAs);
    }

    [Fact]
    public async Task SaveAs_PersistsEnvelope_AndSetsLineage()
    {
        _form.Title = "Chill run";
        _dialogs.SaveAsNames.Enqueue("Elden Ring");

        await _vm.SaveAsCommand.ExecuteAsync(null);

        var reloaded = _store.Load();
        Assert.Equal(1, reloaded.SchemaVersion);
        Assert.Single(reloaded.Presets);
        Assert.Equal("Elden Ring", reloaded.Presets[0].Name);
        Assert.Equal("Chill run", reloaded.Presets[0].Title);

        Assert.True(_form.HasPresetLineage);
        Assert.Equal("Elden Ring", _form.PresetLineage!.Name);
        Assert.False(_form.IsDirtyVsPreset);
        Assert.True(_vm.ShowUpdateButton);
        Assert.Equal("Update preset \"Elden Ring\"", _vm.UpdateButtonText);
    }

    [Fact]
    public async Task SaveAs_Cancelled_LeavesStoreAndLineageUnchanged()
    {
        _dialogs.SaveAsNames.Enqueue(null);

        await _vm.SaveAsCommand.ExecuteAsync(null);

        Assert.Empty(_store.Load().Presets);
        Assert.Null(_form.PresetLineage);
    }

    [Fact]
    public async Task SaveAs_DuplicateName_PromptsReplace_AndOverwritesWhenConfirmed()
    {
        _form.Title = "first";
        _dialogs.SaveAsNames.Enqueue("Shared");
        await _vm.SaveAsCommand.ExecuteAsync(null);

        var firstId = _form.PresetLineage!.Id;

        _form.Title = "second";
        _dialogs.SaveAsNames.Enqueue("shared"); // same name, different case
        _dialogs.ConfirmReplaceAnswers.Enqueue(true);
        await _vm.SaveAsCommand.ExecuteAsync(null);

        var reloaded = _store.Load();
        Assert.Single(reloaded.Presets);
        Assert.Equal(firstId, reloaded.Presets[0].Id);
        Assert.Equal("Shared", reloaded.Presets[0].Name); // preserves the original casing
        Assert.Equal("second", reloaded.Presets[0].Title);
        Assert.Contains("ConfirmReplace:Shared", _dialogs.Calls);
    }

    [Fact]
    public async Task SaveAs_DuplicateName_ReplaceDeclined_LeavesStoreUnchanged()
    {
        _form.Title = "first";
        _dialogs.SaveAsNames.Enqueue("One");
        await _vm.SaveAsCommand.ExecuteAsync(null);

        _form.Title = "second";
        _dialogs.SaveAsNames.Enqueue("One");
        _dialogs.ConfirmReplaceAnswers.Enqueue(false);
        await _vm.SaveAsCommand.ExecuteAsync(null);

        var reloaded = _store.Load();
        Assert.Single(reloaded.Presets);
        Assert.Equal("first", reloaded.Presets[0].Title);
    }

    [Fact]
    public async Task Update_OverwritesLineagePreset_AndResetsDirtyVsPreset()
    {
        _form.Title = "original";
        _dialogs.SaveAsNames.Enqueue("P");
        await _vm.SaveAsCommand.ExecuteAsync(null);

        _form.Title = "edited";
        Assert.True(_form.IsDirtyVsPreset);
        Assert.True(_vm.CanUpdate);

        _vm.UpdateCurrentCommand.Execute(null);

        var reloaded = _store.Load();
        Assert.Equal("edited", reloaded.Presets[0].Title);
        Assert.False(_form.IsDirtyVsPreset);
        Assert.Equal("P", _form.PresetLineage!.Name);
    }

    [Fact]
    public async Task Load_DirtyForm_PromptsConfirm_AndCancellingLeavesForm()
    {
        _form.Title = "preset title";
        _dialogs.SaveAsNames.Enqueue("P");
        await _vm.SaveAsCommand.ExecuteAsync(null);

        // Simulate live baseline, then user edits.
        _form.SetLiveBaseline(_form.CaptureSnapshot());
        _form.Title = "user typed over";
        Assert.True(_form.IsDirtyVsLive);

        var saved = _store.Load().Presets[0];
        _dialogs.ConfirmLoadAnswers.Enqueue(false);

        await _vm.LoadPicker.LoadCommand.ExecuteAsync(saved);

        Assert.Equal("user typed over", _form.Title);
        Assert.Contains($"ConfirmLoadOverDirtyForm:{saved.Name}", _dialogs.Calls);
    }

    [Fact]
    public async Task Load_DirtyForm_ConfirmingReplacesFormAndSetsLineage()
    {
        _form.Title = "preset title";
        _dialogs.SaveAsNames.Enqueue("P");
        await _vm.SaveAsCommand.ExecuteAsync(null);

        _form.SetLiveBaseline(_form.CaptureSnapshot());
        _form.Title = "dirty edit";

        var saved = _store.Load().Presets[0];
        _dialogs.ConfirmLoadAnswers.Enqueue(true);

        await _vm.LoadPicker.LoadCommand.ExecuteAsync(saved);

        Assert.Equal("preset title", _form.Title);
        Assert.Equal(saved.Id, _form.PresetLineage!.Id);
        Assert.False(_form.IsDirtyVsPreset);
    }

    [Fact]
    public async Task Delete_PromptsConfirm_AndRemovesFromStore()
    {
        _dialogs.SaveAsNames.Enqueue("A");
        await _vm.SaveAsCommand.ExecuteAsync(null);
        _form.Title = "new form";
        _dialogs.SaveAsNames.Enqueue("B");
        await _vm.SaveAsCommand.ExecuteAsync(null);

        var toDelete = _store.Load().Presets.Single(p => p.Name == "A");
        _dialogs.ConfirmDeleteAnswers.Enqueue(true);

        await _vm.LoadPicker.DeleteCommand.ExecuteAsync(toDelete);

        var reloaded = _store.Load();
        Assert.Single(reloaded.Presets);
        Assert.Equal("B", reloaded.Presets[0].Name);
    }

    [Fact]
    public async Task Delete_OfLineagePreset_ClearsLineage()
    {
        _dialogs.SaveAsNames.Enqueue("A");
        await _vm.SaveAsCommand.ExecuteAsync(null);

        var current = _store.Load().Presets[0];
        Assert.Equal(current.Id, _form.PresetLineage!.Id);

        _dialogs.ConfirmDeleteAnswers.Enqueue(true);
        await _vm.LoadPicker.DeleteCommand.ExecuteAsync(current);

        Assert.Null(_form.PresetLineage);
        Assert.False(_vm.ShowUpdateButton);
    }

    [Fact]
    public async Task Delete_Cancelled_LeavesStoreIntact()
    {
        _dialogs.SaveAsNames.Enqueue("A");
        await _vm.SaveAsCommand.ExecuteAsync(null);

        var current = _store.Load().Presets[0];
        _dialogs.ConfirmDeleteAnswers.Enqueue(false);

        await _vm.LoadPicker.DeleteCommand.ExecuteAsync(current);

        Assert.Single(_store.Load().Presets);
        Assert.NotNull(_form.PresetLineage);
    }

    [Fact]
    public void CorruptStore_DisablesSaveAs_AndShowsErrorDialog()
    {
        // Prime a bad file BEFORE the VM is built (so we exercise the
        // startup-load failure path; the ctor loads eagerly).
        File.WriteAllText(_paths.PresetsFilePath, "{ not json");

        var localForm = new StreamFormViewModel();
        var localDialogs = new FakePresetDialogs();
        var localVm = new PresetActionsViewModel(
            new PresetStore(_paths),
            localDialogs,
            localForm,
            _time,
            NullLogger<PresetActionsViewModel>.Instance);

        Assert.False(localVm.StoreIsHealthy);
        Assert.False(localVm.CanSaveAs);
        Assert.NotEmpty(localDialogs.LoadErrorsShown);
    }

    [Fact]
    public void NewerSchema_SurfacesSpecificMessage()
    {
        File.WriteAllText(_paths.PresetsFilePath, "{\"schemaVersion\":99,\"presets\":[]}");

        var localDialogs = new FakePresetDialogs();
        _ = new PresetActionsViewModel(
            new PresetStore(_paths),
            localDialogs,
            new StreamFormViewModel(),
            _time,
            NullLogger<PresetActionsViewModel>.Instance);

        Assert.Contains(
            localDialogs.LoadErrorsShown,
            m => m.Contains("newer version", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTimeProvider(DateTimeOffset now) { _now = now; }
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
