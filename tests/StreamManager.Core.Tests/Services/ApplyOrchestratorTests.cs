using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using StreamManager.App.Services;
using StreamManager.App.ViewModels;
using StreamManager.Core.Auth;
using StreamManager.Core.Youtube;
using Xunit;

namespace StreamManager.Core.Tests.Services;

public class ApplyOrchestratorTests
{
    [Fact]
    public async Task HappyPath_NoThumbnail_UpdatesBroadcastThenVideo_ThenRefetches()
    {
        var harness = NewHarness();
        harness.SeedLiveBroadcast(thumbnailPath: null);

        var result = await harness.Orchestrator.ApplyAsync(CancellationToken.None);

        Assert.Equal(ApplyOutcome.Success, result.Outcome);
        Assert.Equal(1, harness.Client.UpdateBroadcastCalls);
        Assert.Equal(1, harness.Client.UpdateVideoCalls);
        Assert.Equal(0, harness.Uploader.SetThumbnailCalls);
        Assert.True(harness.Fetch.AllowOverwriteOnLastCall);
        Assert.Equal(1, harness.Fetch.CallCount);
        Assert.Equal(0, harness.Prompt.PromptCount);

        // Payload-mapping (broadcast resource): values came from the form snapshot.
        var broadcast = harness.Client.LastBroadcastUpdate!;
        Assert.Equal("bc-1", broadcast.BroadcastId);
        Assert.Equal("Form Title", broadcast.Title);
        Assert.Equal("Form Desc", broadcast.Description);
        Assert.Equal("unlisted", broadcast.PrivacyStatus);
        Assert.True(broadcast.SelfDeclaredMadeForKids);
        Assert.Equal(1500, broadcast.BroadcastStreamDelayMs);
        Assert.Equal("low", broadcast.LatencyPreference);
        Assert.Equal("360", broadcast.Projection);
        Assert.Equal("left_right", broadcast.StereoLayout);
        Assert.Equal("closedCaptionsHttpPost", broadcast.ClosedCaptionsType);
        Assert.False(broadcast.EnableDvr);
        Assert.False(broadcast.EnableEmbed);
        Assert.True(broadcast.EnableClosedCaptions);
        Assert.False(broadcast.EnableAutoStart);
        Assert.False(broadcast.EnableAutoStop);
        Assert.False(broadcast.RecordFromStart);
        Assert.True(broadcast.StartWithSlate);
        Assert.True(broadcast.EnableContentEncryption);
        Assert.True(broadcast.EnableLowLatency);
        Assert.False(broadcast.EnableMonitorStream);

        // Payload-mapping (video resource).
        var video = harness.Client.LastVideoUpdate!;
        Assert.Equal("vid-1", video.VideoId);
        Assert.Equal("Form Title", video.Title);
        Assert.Equal("22", video.CategoryId);
        Assert.Equal(new[] { "alpha", "beta" }, video.Tags);
        Assert.Equal("en", video.DefaultLanguage);
        Assert.Equal("de", video.DefaultAudioLanguage);
    }

    [Fact]
    public async Task HappyPath_ReachableThumbnail_Changed_CallsThumbnailUploadOnce()
    {
        var harness = NewHarness();
        harness.SeedLiveBroadcast(thumbnailPath: null);
        harness.Form.ThumbnailPath = "/tmp/thumb.jpg"; // user picked one (changed vs live)
        harness.Checker.Reachable["/tmp/thumb.jpg"] = true;

        var result = await harness.Orchestrator.ApplyAsync(CancellationToken.None);

        Assert.Equal(ApplyOutcome.Success, result.Outcome);
        Assert.Equal(1, harness.Client.UpdateBroadcastCalls);
        Assert.Equal(1, harness.Client.UpdateVideoCalls);
        Assert.Equal(1, harness.Uploader.SetThumbnailCalls);
        Assert.Equal("vid-1", harness.Uploader.LastVideoId);
        Assert.Equal("/tmp/thumb.jpg", harness.Uploader.LastFilePath);
        Assert.Equal(0, harness.Prompt.PromptCount);
        Assert.True(harness.Fetch.AllowOverwriteOnLastCall);
    }

    [Fact]
    public async Task HappyPath_ReachableThumbnail_Unchanged_SkipsThumbnailUpload()
    {
        var harness = NewHarness();
        // Live baseline already has /tmp/thumb.jpg and form matches (unchanged).
        harness.SeedLiveBroadcast(thumbnailPath: "/tmp/thumb.jpg");
        harness.Checker.Reachable["/tmp/thumb.jpg"] = true;

        var result = await harness.Orchestrator.ApplyAsync(CancellationToken.None);

        Assert.Equal(ApplyOutcome.Success, result.Outcome);
        Assert.Equal(0, harness.Uploader.SetThumbnailCalls);
        // Pre-flight does still run (the path is set), but no prompt because reachable.
        Assert.Equal(0, harness.Prompt.PromptCount);
    }

    [Fact]
    public async Task UnreachableThumbnail_PromptApplyWithout_ProceedsWithoutSetThumbnail()
    {
        var harness = NewHarness();
        harness.SeedLiveBroadcast(thumbnailPath: null);
        harness.Form.ThumbnailPath = "/external/missing.jpg";
        // Not added to Checker.Reachable → reports false.
        harness.Prompt.Decision = UnreachableThumbnailDecision.ApplyWithoutThumbnail;

        var result = await harness.Orchestrator.ApplyAsync(CancellationToken.None);

        Assert.Equal(ApplyOutcome.Success, result.Outcome);
        Assert.Equal(1, harness.Prompt.PromptCount);
        Assert.Equal("/external/missing.jpg", harness.Prompt.LastPath);
        Assert.Equal(1, harness.Client.UpdateBroadcastCalls);
        Assert.Equal(1, harness.Client.UpdateVideoCalls);
        Assert.Equal(0, harness.Uploader.SetThumbnailCalls);
        // §6.6 step 1: never auto-clear the thumbnailPath reference.
        Assert.Equal("/external/missing.jpg", harness.Form.ThumbnailPath);
    }

    [Fact]
    public async Task UnreachableThumbnail_PromptCancel_AbortsBeforeAnyApiCall()
    {
        var harness = NewHarness();
        harness.SeedLiveBroadcast(thumbnailPath: null);
        harness.Form.ThumbnailPath = "/external/missing.jpg";
        harness.Prompt.Decision = UnreachableThumbnailDecision.Cancel;

        var result = await harness.Orchestrator.ApplyAsync(CancellationToken.None);

        Assert.Equal(ApplyOutcome.Cancelled, result.Outcome);
        Assert.Equal(1, harness.Prompt.PromptCount);
        Assert.Equal(0, harness.Client.UpdateBroadcastCalls);
        Assert.Equal(0, harness.Client.UpdateVideoCalls);
        Assert.Equal(0, harness.Uploader.SetThumbnailCalls);
        Assert.Equal(0, harness.Fetch.CallCount);
        Assert.Equal("Cancelled by user.", result.ErrorMessage);
        // No-auto-clear assertion (§6.6 step 1).
        Assert.Equal("/external/missing.jpg", harness.Form.ThumbnailPath);
    }

    [Fact]
    public async Task UpdateBroadcastThrows_EmitsErrorCard_VideoNotInvoked()
    {
        var harness = NewHarness();
        harness.SeedLiveBroadcast(thumbnailPath: null);
        harness.Client.ThrowOnUpdateBroadcast = new InvalidOperationException("boom-bc");

        var result = await harness.Orchestrator.ApplyAsync(CancellationToken.None);

        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
        Assert.Equal(ApplyStep.UpdateBroadcast, result.FailedStep);
        Assert.Contains("Step 1", result.ErrorMessage);
        Assert.Contains("boom-bc", result.ErrorMessage);
        Assert.Equal(0, harness.Client.UpdateVideoCalls);
        Assert.Equal(0, harness.Fetch.CallCount); // no re-fetch on step-1 fail
    }

    [Fact]
    public async Task UpdateVideoThrowsAfterBroadcastSucceeds_ReportsPartial_NoRefetch()
    {
        var harness = NewHarness();
        harness.SeedLiveBroadcast(thumbnailPath: null);
        harness.Client.ThrowOnUpdateVideo = new InvalidOperationException("boom-vid");

        var result = await harness.Orchestrator.ApplyAsync(CancellationToken.None);

        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
        Assert.Equal(ApplyStep.UpdateVideo, result.FailedStep);
        Assert.True(result.BroadcastUpdated);
        Assert.Contains("Step 2", result.ErrorMessage);
        Assert.Contains("broadcast-level update did succeed", result.ErrorMessage);
        Assert.Equal(0, harness.Fetch.CallCount); // design says no re-fetch in this case
    }

    [Fact]
    public async Task ThumbnailUploadThrows_EmitsStep3Error_StillRefetches()
    {
        var harness = NewHarness();
        harness.SeedLiveBroadcast(thumbnailPath: null);
        harness.Form.ThumbnailPath = "/tmp/thumb.jpg";
        harness.Checker.Reachable["/tmp/thumb.jpg"] = true;
        harness.Uploader.Throw = new InvalidOperationException("boom-thumb");

        var result = await harness.Orchestrator.ApplyAsync(CancellationToken.None);

        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
        Assert.Equal(ApplyStep.UpdateThumbnail, result.FailedStep);
        Assert.Contains("Step 3", result.ErrorMessage);
        Assert.Contains("boom-thumb", result.ErrorMessage);
        // Still re-fetched so user sees YouTube's state.
        Assert.Equal(1, harness.Fetch.CallCount);
        Assert.True(harness.Fetch.AllowOverwriteOnLastCall);
        // ThumbnailPath not cleared on failure either.
        Assert.Equal("/tmp/thumb.jpg", harness.Form.ThumbnailPath);
    }

    [Fact]
    public async Task ThumbnailFileDisappearsBetweenPreflightAndUpload_ReportsStep3_DoesNotClearPath()
    {
        // Race case from slice-8 acceptance: pre-flight sees reachable, but the
        // file is deleted / drive unmounts between step 1 and step 4. The
        // uploader throws FileNotFoundException, which must surface as a
        // step-3 failure without mutating ThumbnailPath.
        var harness = NewHarness();
        harness.SeedLiveBroadcast(thumbnailPath: null);
        harness.Form.ThumbnailPath = "/tmp/raced.jpg";
        harness.Checker.Reachable["/tmp/raced.jpg"] = true;
        harness.Uploader.Throw = new FileNotFoundException("gone", "/tmp/raced.jpg");

        var result = await harness.Orchestrator.ApplyAsync(CancellationToken.None);

        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
        Assert.Equal(ApplyStep.UpdateThumbnail, result.FailedStep);
        Assert.Contains("Step 3", result.ErrorMessage);
        // Critical design invariant: ThumbnailPath is never mutated by Apply.
        Assert.Equal("/tmp/raced.jpg", harness.Form.ThumbnailPath);
    }

    [Fact]
    public async Task NullScheduledTimes_PassThroughAsNull()
    {
        var harness = NewHarness();
        harness.SeedLiveBroadcast(thumbnailPath: null);
        harness.Form.ScheduledStartTimeText = "";
        harness.Form.ScheduledEndTimeText = "";

        var result = await harness.Orchestrator.ApplyAsync(CancellationToken.None);

        Assert.Equal(ApplyOutcome.Success, result.Outcome);
        Assert.Null(harness.Client.LastBroadcastUpdate!.ScheduledStartTime);
        Assert.Null(harness.Client.LastBroadcastUpdate!.ScheduledEndTime);
    }

    [Fact]
    public async Task NoActiveBroadcast_FailsBeforeAnyApiCall()
    {
        var harness = NewHarness();
        // Don't seed: form has no broadcast/video IDs.
        harness.Form.HasLiveBroadcast = false;

        var result = await harness.Orchestrator.ApplyAsync(CancellationToken.None);

        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
        Assert.Equal(ApplyStep.None, result.FailedStep);
        Assert.Equal(0, harness.Client.UpdateBroadcastCalls);
        Assert.Contains("No active broadcast", result.ErrorMessage);
    }

    [Fact]
    public async Task FormHasValidationErrors_FailsBeforeAnyApiCall()
    {
        var harness = NewHarness();
        harness.SeedLiveBroadcast(thumbnailPath: null);
        // Title > 100 chars triggers MaxLength validation.
        harness.Form.Title = new string('x', 200);

        var result = await harness.Orchestrator.ApplyAsync(CancellationToken.None);

        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
        Assert.Equal(0, harness.Client.UpdateBroadcastCalls);
        Assert.Contains("validation", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReentrancyGuard_ConcurrentApplyIsNoOp()
    {
        var harness = NewHarness();
        harness.SeedLiveBroadcast(thumbnailPath: null);
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Client.UpdateBroadcastDelay = release.Task;

        var first = harness.Orchestrator.ApplyAsync(CancellationToken.None);
        // Yield so first definitely entered the running section.
        await Task.Yield();
        var second = await harness.Orchestrator.ApplyAsync(CancellationToken.None);

        Assert.Equal(ApplyOutcome.Cancelled, second.Outcome);
        Assert.Contains("already in progress", second.ErrorMessage);

        release.SetResult(null);
        var firstResult = await first;
        Assert.Equal(ApplyOutcome.Success, firstResult.Outcome);
        Assert.Equal(1, harness.Client.UpdateBroadcastCalls);
    }

    // ---- Harness ----

    private static Harness NewHarness() => new();

    private sealed class Harness
    {
        public StreamFormViewModel Form { get; } = new();
        public FakeYouTubeWriteClient Client { get; } = new();
        public FakeThumbnailUploader Uploader { get; } = new();
        public StubThumbnailChecker Checker { get; } = new();
        public StubUnreachablePrompt Prompt { get; } = new();
        public PassthroughReauth Reauth { get; } = new();
        public RecordingFetchCoordinator Fetch { get; } = new();
        public ApplyOrchestrator Orchestrator { get; }

        public Harness()
        {
            Orchestrator = new ApplyOrchestrator(
                Form,
                Client,
                Uploader,
                Checker,
                Prompt,
                Reauth,
                Fetch,
                NullLogger<ApplyOrchestrator>.Instance);
        }

        // Sets the form to a representative live-broadcast baseline whose
        // values differ from the StreamFormViewModel defaults so the
        // payload-mapping tests can assert each field came from the form.
        public void SeedLiveBroadcast(string? thumbnailPath)
        {
            var snapshot = new StreamFormSnapshot
            {
                Title = "Form Title",
                Description = "Form Desc",
                CategoryId = "22",
                Tags = new[] { "alpha", "beta" },
                PrivacyStatus = "unlisted",
                SelfDeclaredMadeForKids = true,
                EnableAutoStart = false,
                EnableAutoStop = false,
                EnableClosedCaptions = true,
                EnableDvr = false,
                EnableEmbed = false,
                RecordFromStart = false,
                StartWithSlate = true,
                EnableContentEncryption = true,
                EnableLowLatency = true,
                LatencyPreference = "low",
                EnableMonitorStream = false,
                BroadcastStreamDelayMs = 1500,
                Projection = "360",
                StereoLayout = "left_right",
                ClosedCaptionsType = "closedCaptionsHttpPost",
                ScheduledStartTime = DateTimeOffset.Parse("2026-04-23T18:00:00Z"),
                ScheduledEndTime = DateTimeOffset.Parse("2026-04-23T21:00:00Z"),
                DefaultLanguage = "en",
                DefaultAudioLanguage = "de",
                ThumbnailPath = thumbnailPath,
            };
            Form.SetLiveBaseline(snapshot, "bc-1", "vid-1");
            Form.HasLiveBroadcast = true;
            Form.IsConnected = true;
        }
    }

    private sealed class FakeYouTubeWriteClient : IYouTubeClient
    {
        public int UpdateBroadcastCalls { get; private set; }
        public int UpdateVideoCalls { get; private set; }
        public BroadcastUpdate? LastBroadcastUpdate { get; private set; }
        public VideoUpdate? LastVideoUpdate { get; private set; }
        public Exception? ThrowOnUpdateBroadcast { get; set; }
        public Exception? ThrowOnUpdateVideo { get; set; }
        public Task? UpdateBroadcastDelay { get; set; }

        public Task<BroadcastSnapshot?> GetActiveBroadcastAsync(CancellationToken ct) =>
            throw new NotSupportedException("Apply tests do not call GetActiveBroadcastAsync.");

        public async Task UpdateBroadcastAsync(BroadcastUpdate update, CancellationToken ct)
        {
            if (UpdateBroadcastDelay is not null) await UpdateBroadcastDelay;
            UpdateBroadcastCalls++;
            LastBroadcastUpdate = update;
            if (ThrowOnUpdateBroadcast is not null) throw ThrowOnUpdateBroadcast;
        }

        public Task UpdateVideoAsync(VideoUpdate update, CancellationToken ct)
        {
            UpdateVideoCalls++;
            LastVideoUpdate = update;
            if (ThrowOnUpdateVideo is not null) throw ThrowOnUpdateVideo;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<VideoCategoryListItem>> ListVideoCategoriesAsync(
            string regionCode, CancellationToken ct) =>
            throw new NotSupportedException("Apply tests do not call ListVideoCategoriesAsync.");

        public Task<IReadOnlyList<I18nLanguageListItem>> ListI18nLanguagesAsync(
            CancellationToken ct) =>
            throw new NotSupportedException("Apply tests do not call ListI18nLanguagesAsync.");
    }

    private sealed class FakeThumbnailUploader : IThumbnailUploader
    {
        public int SetThumbnailCalls { get; private set; }
        public string? LastVideoId { get; private set; }
        public string? LastFilePath { get; private set; }
        public Exception? Throw { get; set; }

        public Task SetThumbnailAsync(string videoId, string filePath, CancellationToken ct)
        {
            SetThumbnailCalls++;
            LastVideoId = videoId;
            LastFilePath = filePath;
            if (Throw is not null) throw Throw;
            return Task.CompletedTask;
        }
    }

    private sealed class StubThumbnailChecker : IThumbnailFileChecker
    {
        public Dictionary<string, bool> Reachable { get; } = new();
        public bool IsReachable(string path) => Reachable.TryGetValue(path, out var ok) && ok;
    }

    private sealed class StubUnreachablePrompt : IUnreachableThumbnailPrompt
    {
        public UnreachableThumbnailDecision Decision { get; set; } = UnreachableThumbnailDecision.Cancel;
        public int PromptCount { get; private set; }
        public string? LastPath { get; private set; }

        public Task<UnreachableThumbnailDecision> PromptAsync(string thumbnailPath, CancellationToken ct)
        {
            PromptCount++;
            LastPath = thumbnailPath;
            return Task.FromResult(Decision);
        }
    }

    private sealed class PassthroughReauth : IReauthOrchestrator
    {
        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            ReauthOperationOptions options,
            CancellationToken ct) =>
            await operation(ct);

        public async Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            ReauthOperationOptions options,
            CancellationToken ct)
        {
            await operation(ct);
        }
    }

    private sealed class RecordingFetchCoordinator : IStreamFetchCoordinator
    {
        public int CallCount { get; private set; }
        public bool AllowOverwriteOnLastCall { get; private set; }

        public Task<StreamFetchResult> FetchAsync(bool allowOverwrite, CancellationToken ct)
        {
            CallCount++;
            AllowOverwriteOnLastCall = allowOverwrite;
            return Task.FromResult(new StreamFetchResult(
                StreamFetchOutcome.Live,
                LiveIndicatorStatus.Live));
        }
    }
}
