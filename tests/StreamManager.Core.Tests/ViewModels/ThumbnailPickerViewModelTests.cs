using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using StreamManager.App.Services;
using StreamManager.App.ViewModels;
using Xunit;

namespace StreamManager.Core.Tests.ViewModels;

public class ThumbnailPickerViewModelTests
{
    [Fact]
    public async Task Pick_ValidFile_SetsFormPathAndPreview()
    {
        var h = NewHarness();
        h.Picker.NextResult = "/tmp/good.jpg";
        h.Validator.Result = ThumbnailValidationIssue.Ok;
        h.Checker.Reachable["/tmp/good.jpg"] = true;

        await h.Vm.PickCommand.ExecuteAsync(null);

        Assert.Equal("/tmp/good.jpg", h.Form.ThumbnailPath);
        Assert.Equal("/tmp/good.jpg", h.Vm.LocalPreviewPath);
        Assert.True(h.Vm.HasLocalPreview);
        Assert.False(h.Vm.ShowUnreachablePlaceholder);
        Assert.Equal(0, h.Prompt.ShowCount);
    }

    [Fact]
    public async Task Pick_OversizeFile_PromptsAndLeavesStateUnchanged()
    {
        var h = NewHarness();
        h.Picker.NextResult = "/tmp/huge.jpg";
        h.Validator.Result = ThumbnailValidationIssue.TooLarge;

        await h.Vm.PickCommand.ExecuteAsync(null);

        Assert.Null(h.Form.ThumbnailPath);
        Assert.Null(h.Vm.LocalPreviewPath);
        Assert.Equal(1, h.Prompt.ShowCount);
        Assert.Equal(ThumbnailValidationIssue.TooLarge, h.Prompt.LastIssue);
        Assert.Equal("/tmp/huge.jpg", h.Prompt.LastPath);
    }

    [Fact]
    public async Task Pick_BadExtension_PromptsAndLeavesStateUnchanged()
    {
        var h = NewHarness();
        h.Picker.NextResult = "/tmp/thing.tiff";
        h.Validator.Result = ThumbnailValidationIssue.BadExtension;

        await h.Vm.PickCommand.ExecuteAsync(null);

        Assert.Null(h.Form.ThumbnailPath);
        Assert.Equal(1, h.Prompt.ShowCount);
        Assert.Equal(ThumbnailValidationIssue.BadExtension, h.Prompt.LastIssue);
    }

    [Fact]
    public async Task Pick_Cancelled_NoStateChange_NoPrompt()
    {
        var h = NewHarness();
        h.Picker.NextResult = null;

        await h.Vm.PickCommand.ExecuteAsync(null);

        Assert.Null(h.Form.ThumbnailPath);
        Assert.Equal(0, h.Prompt.ShowCount);
        Assert.Equal(0, h.Validator.CallCount);
    }

    [Fact]
    public async Task Clear_AfterPick_ClearsPathAndPreview()
    {
        var h = NewHarness();
        h.Form.ThumbnailPath = "/tmp/good.jpg";
        h.Checker.Reachable["/tmp/good.jpg"] = true;
        Assert.True(h.Vm.HasLocalPreview);

        h.Vm.ClearCommand.Execute(null);

        Assert.Null(h.Form.ThumbnailPath);
        Assert.Null(h.Vm.LocalPreviewPath);
        Assert.False(h.Vm.HasLocalPreview);
        Assert.False(h.Vm.ShowUnreachablePlaceholder);
        // Change flag: the form's IsThumbnailChangedFromLive stays the
        // authoritative signal. With no live baseline, clearing to null
        // equals the baseline's null path → "unchanged" — that's fine
        // because there was no live baseline. We're only asserting the
        // VM mutated the form correctly.
        await Task.CompletedTask;
    }

    [Fact]
    public void UnreachablePath_ShowsPlaceholder_WithPathText()
    {
        var h = NewHarness();
        h.Form.ThumbnailPath = "/external/missing.jpg";
        // Not added to Reachable map → reports false.

        Assert.True(h.Vm.ShowUnreachablePlaceholder);
        Assert.Equal("/external/missing.jpg", h.Vm.UnreachablePathText);
        Assert.False(h.Vm.HasLocalPreview);
        Assert.Null(h.Vm.LocalPreviewPath);
    }

    [Fact]
    public void FormPathChange_PropagatesPreviewStateChanges()
    {
        var h = NewHarness();
        var changed = new List<string?>();
        h.Vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        h.Form.ThumbnailPath = "/tmp/x.jpg"; // not reachable

        Assert.Contains(nameof(h.Vm.LocalPreviewPath), changed);
        Assert.Contains(nameof(h.Vm.ShowUnreachablePlaceholder), changed);
        Assert.Contains(nameof(h.Vm.UnreachablePathText), changed);
        Assert.Contains(nameof(h.Vm.HasThumbnailPath), changed);
    }

    [Fact]
    public void FormThumbnailChangeFromLive_LoadedPresetCountsAsChanged()
    {
        // Acceptance: "Loading a preset that has a `thumbnailPath` counts as
        // a thumbnail change if it differs from the current live thumbnail."
        // The live baseline's ThumbnailPath is always null after a fetch
        // (the API returns a remote URL, not a local path), so a preset that
        // sets ThumbnailPath to any non-null value is a change.
        var h = NewHarness();
        var baseline = new StreamFormSnapshot { ThumbnailPath = null };
        h.Form.SetLiveBaseline(baseline);

        h.Form.ThumbnailPath = "/preset/pic.jpg";

        Assert.True(h.Form.IsThumbnailChangedFromLive);
    }

    // ---- Harness ----

    private static Harness NewHarness() => new();

    private sealed class Harness
    {
        public StreamFormViewModel Form { get; } = new();
        public StubPicker Picker { get; } = new();
        public StubValidator Validator { get; } = new();
        public StubPrompt Prompt { get; } = new();
        public StubChecker Checker { get; } = new();
        public ThumbnailPickerViewModel Vm { get; }

        public Harness()
        {
            Vm = new ThumbnailPickerViewModel(
                Form, Picker, Validator, Prompt, Checker,
                NullLogger<ThumbnailPickerViewModel>.Instance);
        }
    }

    private sealed class StubPicker : IThumbnailPickerService
    {
        public string? NextResult { get; set; }
        public int CallCount { get; private set; }

        public Task<string?> PickFileAsync(CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class StubValidator : IThumbnailFileValidator
    {
        public ThumbnailValidationIssue Result { get; set; } = ThumbnailValidationIssue.Ok;
        public int CallCount { get; private set; }
        public string? LastPath { get; private set; }

        public ThumbnailValidationIssue Validate(string path)
        {
            CallCount++;
            LastPath = path;
            return Result;
        }
    }

    private sealed class StubPrompt : IThumbnailValidationPrompt
    {
        public int ShowCount { get; private set; }
        public ThumbnailValidationIssue LastIssue { get; private set; }
        public string? LastPath { get; private set; }

        public Task ShowAsync(ThumbnailValidationIssue issue, string path, CancellationToken ct)
        {
            ShowCount++;
            LastIssue = issue;
            LastPath = path;
            return Task.CompletedTask;
        }
    }

    private sealed class StubChecker : IThumbnailFileChecker
    {
        public Dictionary<string, bool> Reachable { get; } = new();
        public bool IsReachable(string path) => Reachable.TryGetValue(path, out var ok) && ok;
    }
}
