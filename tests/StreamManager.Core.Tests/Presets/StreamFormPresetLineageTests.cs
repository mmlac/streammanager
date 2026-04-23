using StreamManager.App.ViewModels;
using Xunit;

namespace StreamManager.Core.Tests.Presets;

// Complements StreamFormViewModelTests — focused on the preset-lineage
// transitions slice 7 introduces (save-as, rebaseline after update,
// delete-clears-lineage is exercised against the orchestrator).
public class StreamFormPresetLineageTests
{
    [Fact]
    public void SetPresetBaselineFromCurrent_AdoptsFormAsBaseline_AndSetsLineage()
    {
        var vm = new StreamFormViewModel();
        vm.Title = "current title";
        vm.BroadcastStreamDelayMs = 1234;

        vm.SetPresetBaselineFromCurrent(new PresetLineage("p1", "My preset"));

        Assert.Equal("current title", vm.Title);
        Assert.Equal(1234, vm.BroadcastStreamDelayMs);
        Assert.False(vm.IsDirtyVsPreset);
        Assert.True(vm.HasPresetLineage);
        Assert.Equal("My preset", vm.PresetLineage!.Name);
    }

    [Fact]
    public void EditAfterSetPresetBaselineFromCurrent_FlipsDirtyVsPreset()
    {
        var vm = new StreamFormViewModel();
        vm.Title = "initial";
        vm.SetPresetBaselineFromCurrent(new PresetLineage("p1", "P"));

        vm.Title = "edited";
        Assert.True(vm.IsDirtyVsPreset);
        Assert.True(vm.CanUpdatePreset);
    }

    [Fact]
    public void RebaselineCurrentPreset_ClearsDirtyVsPreset_AndKeepsLineage()
    {
        var vm = new StreamFormViewModel();
        vm.SetPresetBaselineFromCurrent(new PresetLineage("p1", "P"));
        vm.Title = "edited";
        Assert.True(vm.IsDirtyVsPreset);

        vm.RebaselineCurrentPreset();

        Assert.False(vm.IsDirtyVsPreset);
        Assert.Equal("edited", vm.Title);
        Assert.Equal("p1", vm.PresetLineage!.Id);
    }

    [Fact]
    public void RebaselineCurrentPreset_IsNoOpWithoutLineage()
    {
        var vm = new StreamFormViewModel();
        vm.Title = "x";

        vm.RebaselineCurrentPreset();

        Assert.Null(vm.PresetLineage);
        Assert.False(vm.IsDirtyVsPreset);
    }

    [Fact]
    public void SetPresetBaseline_ReplacesFormValues_AndClearsDirtyVsPreset()
    {
        var vm = new StreamFormViewModel();
        vm.Title = "user typed";

        var snap = new StreamFormSnapshot { Title = "from preset", BroadcastStreamDelayMs = 500 };
        vm.SetPresetBaseline(snap, new PresetLineage("p1", "P"));

        Assert.Equal("from preset", vm.Title);
        Assert.Equal(500, vm.BroadcastStreamDelayMs);
        Assert.False(vm.IsDirtyVsPreset);
        Assert.True(vm.HasPresetLineage);
    }

    [Fact]
    public void DirtyStatusLine_IncludesLineageName_WhenPresetLoaded()
    {
        var vm = new StreamFormViewModel();
        vm.SetPresetBaselineFromCurrent(new PresetLineage("p1", "Elden Ring"));

        Assert.Contains("Elden Ring", vm.DirtyStatusLine);
    }
}
