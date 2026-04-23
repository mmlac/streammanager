using System.Linq;
using StreamManager.App.ViewModels;
using Xunit;

namespace StreamManager.Core.Tests;

public class StreamFormViewModelTests
{
    // ---- Baselines ----

    [Fact]
    public void FreshForm_IsNotDirty_AndHasNoLineage()
    {
        var vm = new StreamFormViewModel();

        Assert.False(vm.IsDirtyVsLive);
        Assert.False(vm.IsDirtyVsPreset);
        Assert.Null(vm.PresetLineage);
        Assert.False(vm.HasPresetLineage);
        Assert.False(vm.CanUpdatePreset);
    }

    [Fact]
    public void SetLiveBaseline_CopiesValues_AndClearsDirtyVsLive()
    {
        var vm = new StreamFormViewModel();
        vm.Title = "edited before baseline";
        Assert.False(vm.IsDirtyVsLive); // no live baseline yet

        var snap = vm.CaptureSnapshot() with { Title = "baseline title" };
        vm.SetLiveBaseline(snap);

        Assert.Equal("baseline title", vm.Title);
        Assert.False(vm.IsDirtyVsLive);

        vm.Title = "new edit";
        Assert.True(vm.IsDirtyVsLive);
    }

    [Fact]
    public void SetPresetBaseline_StoresLineage_AndClearsPresetDirty()
    {
        var vm = new StreamFormViewModel();
        var snap = vm.CaptureSnapshot() with { Title = "preset title" };
        var lineage = new PresetLineage("preset-1", "Elden Ring — chill");

        vm.SetPresetBaseline(snap, lineage);

        Assert.Equal("preset title", vm.Title);
        Assert.False(vm.IsDirtyVsPreset);
        Assert.Equal(lineage, vm.PresetLineage);
        Assert.True(vm.HasPresetLineage);
    }

    [Fact]
    public void EditingAfterPresetBaseline_FlipsIsDirtyVsPreset()
    {
        var vm = new StreamFormViewModel();
        vm.SetPresetBaseline(vm.CaptureSnapshot(), new PresetLineage("p", "P"));

        vm.EnableDvr = !vm.EnableDvr;

        Assert.True(vm.IsDirtyVsPreset);
        Assert.True(vm.CanUpdatePreset);
    }

    [Fact]
    public void TogglingBooleanBackToBaseline_ClearsDirty()
    {
        var vm = new StreamFormViewModel();
        vm.SetLiveBaseline(vm.CaptureSnapshot());

        var original = vm.EnableDvr;
        vm.EnableDvr = !original;
        Assert.True(vm.IsDirtyVsLive);

        vm.EnableDvr = original;
        Assert.False(vm.IsDirtyVsLive);
    }

    [Fact]
    public void EditingThenUndoingAllFields_ReturnsFormToClean()
    {
        var vm = new StreamFormViewModel();
        vm.SetLiveBaseline(vm.CaptureSnapshot());

        vm.Title = "A";
        vm.Description = "B";
        vm.BroadcastStreamDelayMs = 1234;
        Assert.True(vm.IsDirtyVsLive);

        vm.Title = "";
        vm.Description = "";
        vm.BroadcastStreamDelayMs = 0;
        Assert.False(vm.IsDirtyVsLive);
    }

    // ---- Validation: title ----

    [Theory]
    [InlineData(0, false)]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void TitleLengthValidation(int length, bool expectError)
    {
        var vm = new StreamFormViewModel();
        vm.Title = new string('x', length);

        var hasTitleError = vm.GetErrors(nameof(vm.Title)).Cast<object>().Any();
        Assert.Equal(expectError, hasTitleError);
    }

    // ---- Validation: description ----

    [Theory]
    [InlineData(5000, false)]
    [InlineData(5001, true)]
    public void DescriptionLengthValidation(int length, bool expectError)
    {
        var vm = new StreamFormViewModel();
        vm.Description = new string('x', length);

        var hasError = vm.GetErrors(nameof(vm.Description)).Cast<object>().Any();
        Assert.Equal(expectError, hasError);
    }

    // ---- Validation: tags combined length ----

    [Theory]
    [InlineData(499, false)]
    [InlineData(500, false)]
    [InlineData(501, true)]
    public void TagsCombinedLengthValidation(int totalLength, bool expectError)
    {
        var vm = new StreamFormViewModel();
        // Seed tags directly so we bypass the add-time guard in AddPendingTag.
        vm.Tags.Clear();
        vm.Tags.Add(new string('a', totalLength));

        var hasError = vm.GetErrors(nameof(vm.Tags)).Cast<object>().Any();
        Assert.Equal(expectError, hasError);
    }

    // ---- Validation: broadcastStreamDelayMs ----

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(30000, false)]
    [InlineData(60000, false)]
    [InlineData(60001, true)]
    public void BroadcastStreamDelayValidation(int ms, bool expectError)
    {
        var vm = new StreamFormViewModel();
        vm.BroadcastStreamDelayMs = ms;

        var hasError = vm.GetErrors(nameof(vm.BroadcastStreamDelayMs)).Cast<object>().Any();
        Assert.Equal(expectError, hasError);
    }

    // ---- Validation: enums ----

    [Theory]
    [InlineData("public", false)]
    [InlineData("private", false)]
    [InlineData("whoops", true)]
    public void PrivacyStatusWhitelist(string value, bool expectError)
    {
        var vm = new StreamFormViewModel();
        vm.PrivacyStatus = value;

        Assert.Equal(expectError, vm.GetErrors(nameof(vm.PrivacyStatus)).Cast<object>().Any());
    }

    [Theory]
    [InlineData("normal", false)]
    [InlineData("ultraLow", false)]
    [InlineData("bogus", true)]
    public void LatencyPreferenceWhitelist(string value, bool expectError)
    {
        var vm = new StreamFormViewModel();
        vm.LatencyPreference = value;

        Assert.Equal(expectError, vm.GetErrors(nameof(vm.LatencyPreference)).Cast<object>().Any());
    }

    [Theory]
    [InlineData("rectangular", false)]
    [InlineData("360", false)]
    [InlineData("mesh", false)]
    [InlineData("flat", true)]
    public void ProjectionWhitelist(string value, bool expectError)
    {
        var vm = new StreamFormViewModel();
        vm.Projection = value;

        Assert.Equal(expectError, vm.GetErrors(nameof(vm.Projection)).Cast<object>().Any());
    }

    [Theory]
    [InlineData("mono", false)]
    [InlineData("left_right", false)]
    [InlineData("topbottom", true)]
    public void StereoLayoutWhitelist(string value, bool expectError)
    {
        var vm = new StreamFormViewModel();
        vm.StereoLayout = value;

        Assert.Equal(expectError, vm.GetErrors(nameof(vm.StereoLayout)).Cast<object>().Any());
    }

    [Theory]
    [InlineData("closedCaptionsDisabled", false)]
    [InlineData("closedCaptionsHttpPost", false)]
    [InlineData("maybe", true)]
    public void ClosedCaptionsTypeWhitelist(string value, bool expectError)
    {
        var vm = new StreamFormViewModel();
        vm.ClosedCaptionsType = value;

        Assert.Equal(expectError, vm.GetErrors(nameof(vm.ClosedCaptionsType)).Cast<object>().Any());
    }

    // ---- Validation: scheduled time text ----

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("2026-04-23T18:00:00Z", false)]
    [InlineData("not a date", true)]
    public void ScheduledStartTextValidation(string value, bool expectError)
    {
        var vm = new StreamFormViewModel();
        vm.ScheduledStartTimeText = value;

        Assert.Equal(expectError, vm.GetErrors(nameof(vm.ScheduledStartTimeText)).Cast<object>().Any());
    }

    // ---- Apply gating ----

    [Fact]
    public void CanApply_IsFalseWhenDisconnected_EvenWithValidForm()
    {
        var vm = new StreamFormViewModel();
        Assert.False(vm.IsConnected);
        Assert.False(vm.HasLiveBroadcast);
        Assert.False(vm.HasErrors);
        Assert.False(vm.CanApply);
    }

    [Fact]
    public void CanApply_IsFalseWhenValidationErrorPresent()
    {
        var vm = new StreamFormViewModel();
        vm.Title = new string('x', 101);
        Assert.True(vm.HasErrors);
        Assert.False(vm.CanApply);
    }

    // ---- Tag chip command flows ----

    [Fact]
    public void AddPendingTag_AddsTrimmedInput_AndClearsInput()
    {
        var vm = new StreamFormViewModel();
        vm.PendingTagInput = "  elden ring  ";
        vm.AddPendingTagCommand.Execute(null);

        Assert.Equal(new[] { "elden ring" }, vm.Tags.ToArray());
        Assert.Equal("", vm.PendingTagInput);
    }

    [Fact]
    public void AddPendingTag_IgnoresBlankInput()
    {
        var vm = new StreamFormViewModel();
        vm.PendingTagInput = "   ";
        vm.AddPendingTagCommand.Execute(null);

        Assert.Empty(vm.Tags);
    }

    [Fact]
    public void AddPendingTag_RefusesToExceedCombinedLimit()
    {
        var vm = new StreamFormViewModel();
        vm.Tags.Add(new string('a', 495));

        vm.PendingTagInput = "123456"; // 495 + 6 = 501 > 500
        vm.AddPendingTagCommand.Execute(null);

        Assert.Single(vm.Tags); // add rejected, existing tag remains
        Assert.Equal("123456", vm.PendingTagInput); // input preserved so user can edit
    }

    [Fact]
    public void RemoveTagCommand_RemovesSpecificTag()
    {
        var vm = new StreamFormViewModel();
        vm.Tags.Add("a");
        vm.Tags.Add("b");
        vm.Tags.Add("c");

        vm.RemoveTagCommand.Execute("b");

        Assert.Equal(new[] { "a", "c" }, vm.Tags.ToArray());
    }

    [Fact]
    public void RemoveLastTagCommand_RemovesLastTagOnly()
    {
        var vm = new StreamFormViewModel();
        vm.Tags.Add("a");
        vm.Tags.Add("b");

        vm.RemoveLastTagCommand.Execute(null);

        Assert.Equal(new[] { "a" }, vm.Tags.ToArray());
    }

    [Fact]
    public void RemoveLastTagCommand_OnEmptyCollection_DoesNotThrow()
    {
        var vm = new StreamFormViewModel();
        var ex = Record.Exception(() => vm.RemoveLastTagCommand.Execute(null));
        Assert.Null(ex);
    }

    // ---- Error decoration appear/disappear (INotifyDataErrorInfo surface) ----

    [Fact]
    public void TitleErrors_AppearAndDisappearAsValueCrossesLimit()
    {
        var vm = new StreamFormViewModel();
        Assert.Empty(vm.GetErrors(nameof(vm.Title)).Cast<object>());

        vm.Title = new string('x', 101);
        Assert.NotEmpty(vm.GetErrors(nameof(vm.Title)).Cast<object>());

        vm.Title = new string('x', 50);
        Assert.Empty(vm.GetErrors(nameof(vm.Title)).Cast<object>());
    }

    [Fact]
    public void TitleError_IncludesLimitReferenceInMessage()
    {
        var vm = new StreamFormViewModel();
        vm.Title = new string('x', 101);

        var errs = vm.GetErrors(nameof(vm.Title)).Cast<object>().Select(o => o.ToString() ?? "").ToList();
        Assert.Contains(errs, m => m.Contains("100"));
    }

    [Fact]
    public void DescriptionError_IncludesLimitReferenceInMessage()
    {
        var vm = new StreamFormViewModel();
        vm.Description = new string('x', 5001);

        var errs = vm.GetErrors(nameof(vm.Description)).Cast<object>().Select(o => o.ToString() ?? "").ToList();
        Assert.Contains(errs, m => m.Contains("5000"));
    }

    [Fact]
    public void TagsError_IncludesLimitReferenceInMessage()
    {
        var vm = new StreamFormViewModel();
        vm.Tags.Add(new string('a', 501));

        var errs = vm.GetErrors(nameof(vm.Tags)).Cast<object>().Select(o => o.ToString() ?? "").ToList();
        Assert.Contains(errs, m => m.Contains("500"));
    }

    // ---- Negative tests from bead notes ----

    [Fact]
    public void NullBaseline_DoesNotThrow_AndFormTakesDefaults()
    {
        var vm = new StreamFormViewModel();
        var ex = Record.Exception(() => vm.SetLiveBaseline(null));
        Assert.Null(ex);

        Assert.Equal("", vm.Title);
        Assert.Equal(StreamFormEnums.PrivacyStatuses.Public, vm.PrivacyStatus);
    }

    [Fact]
    public void SetLiveBaselineNull_PreservesPresetLineage()
    {
        var vm = new StreamFormViewModel();
        var lineage = new PresetLineage("p", "P");
        vm.SetPresetBaseline(vm.CaptureSnapshot(), lineage);

        vm.SetLiveBaseline(null);

        Assert.Equal(lineage, vm.PresetLineage);
        Assert.True(vm.HasPresetLineage);
    }

    [Fact]
    public void SetPresetBaseline_AllowsValueEditsToFlipDirtyVsPresetOnly()
    {
        var vm = new StreamFormViewModel();
        // No live baseline — dirty-vs-live stays false.
        vm.SetPresetBaseline(vm.CaptureSnapshot(), new PresetLineage("p", "P"));

        vm.Title = "diverged";

        Assert.False(vm.IsDirtyVsLive);
        Assert.True(vm.IsDirtyVsPreset);
    }

    [Fact]
    public void CaptureSnapshot_RoundTripsThroughSetLiveBaseline()
    {
        var vm = new StreamFormViewModel();
        vm.Title = "t";
        vm.Description = "d";
        vm.Tags.Add("one");
        vm.Tags.Add("two");
        vm.BroadcastStreamDelayMs = 2500;
        vm.LatencyPreference = StreamFormEnums.LatencyPreferences.Low;
        vm.ScheduledStartTimeText = "2026-04-23T18:00:00Z";

        var snap = vm.CaptureSnapshot();

        var vm2 = new StreamFormViewModel();
        vm2.SetLiveBaseline(snap);

        Assert.Equal("t", vm2.Title);
        Assert.Equal("d", vm2.Description);
        Assert.Equal(new[] { "one", "two" }, vm2.Tags.ToArray());
        Assert.Equal(2500, vm2.BroadcastStreamDelayMs);
        Assert.Equal("low", vm2.LatencyPreference);
        Assert.False(vm2.IsDirtyVsLive);
    }
}
