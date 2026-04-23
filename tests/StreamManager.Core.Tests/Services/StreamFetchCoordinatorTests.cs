using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using StreamManager.App.Auth;
using StreamManager.App.Services;
using StreamManager.App.ViewModels;
using StreamManager.Core.Auth;
using StreamManager.Core.Youtube;
using Xunit;

namespace StreamManager.Core.Tests.Services;

public class StreamFetchCoordinatorTests
{
    [Fact]
    public async Task HappyPath_SetsLiveBaselineOnce_AndReturnsLive()
    {
        var snapshot = NewSnapshot();
        var client = new FakeYouTubeClient { Response = snapshot };
        var form = new StreamFormViewModel();
        var c = NewCoordinator(client, form);

        var result = await c.FetchAsync(allowOverwrite: false, CancellationToken.None);

        Assert.Equal(StreamFetchOutcome.Live, result.Outcome);
        Assert.Equal(LiveIndicatorStatus.Live, result.Status);
        Assert.True(form.HasLiveBroadcast);
        Assert.False(form.IsDirtyVsLive); // baseline set ⇒ clean vs live
        Assert.Equal("Title from API", form.Title);
        Assert.Equal("https://img/x.jpg", form.RemoteThumbnailUrl);
        Assert.Same(snapshot, result.Snapshot);
    }

    [Fact]
    public async Task NoActiveBroadcast_LeavesFormUntouched_AndReturnsNotLive()
    {
        var client = new FakeYouTubeClient { Response = null };
        var form = new StreamFormViewModel();
        form.Title = "user edit"; // form state before fetch
        var c = NewCoordinator(client, form);

        var result = await c.FetchAsync(allowOverwrite: false, CancellationToken.None);

        Assert.Equal(StreamFetchOutcome.NotLive, result.Outcome);
        Assert.Equal(LiveIndicatorStatus.NotLive, result.Status);
        Assert.False(form.HasLiveBroadcast);
        Assert.Null(form.RemoteThumbnailUrl);
        // form.Title preserved
        Assert.Equal("user edit", form.Title);
    }

    [Fact]
    public async Task DirtyFormAndNotAllowOverwrite_InvokesGuard_CancelShortCircuits()
    {
        var client = new FakeYouTubeClient { Response = NewSnapshot() };
        var form = DirtyForm();
        var guard = new StubDirtyFormGuard { Response = false };
        var c = NewCoordinator(client, form, guard);

        var result = await c.FetchAsync(allowOverwrite: false, CancellationToken.None);

        Assert.Equal(1, guard.Invocations);
        Assert.Equal(StreamFetchOutcome.Cancelled, result.Outcome);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task DirtyFormAndNotAllowOverwrite_GuardConfirms_ProceedsWithFetch()
    {
        var client = new FakeYouTubeClient { Response = NewSnapshot() };
        var form = DirtyForm();
        var guard = new StubDirtyFormGuard { Response = true };
        var c = NewCoordinator(client, form, guard);

        var result = await c.FetchAsync(allowOverwrite: false, CancellationToken.None);

        Assert.Equal(1, guard.Invocations);
        Assert.Equal(1, client.CallCount);
        Assert.Equal(StreamFetchOutcome.Live, result.Outcome);
    }

    [Fact]
    public async Task AllowOverwriteTrue_BypassesGuard_EvenWhenDirty()
    {
        var client = new FakeYouTubeClient { Response = NewSnapshot() };
        var form = DirtyForm();
        var guard = new StubDirtyFormGuard { Response = false }; // would cancel if called
        var c = NewCoordinator(client, form, guard);

        var result = await c.FetchAsync(allowOverwrite: true, CancellationToken.None);

        Assert.Equal(0, guard.Invocations);
        Assert.Equal(StreamFetchOutcome.Live, result.Outcome);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task ApiException_ReturnsFetchFailed_AndPreservesFormState()
    {
        var client = new FakeYouTubeClient { Throw = new InvalidOperationException("boom") };
        var form = new StreamFormViewModel();
        form.Title = "baseline";
        form.SetLiveBaseline(form.CaptureSnapshot());
        form.HasLiveBroadcast = true; // previously fetched

        var c = NewCoordinator(client, form);

        var result = await c.FetchAsync(allowOverwrite: false, CancellationToken.None);

        Assert.Equal(StreamFetchOutcome.FetchFailed, result.Outcome);
        Assert.Equal(LiveIndicatorStatus.FetchFailed, result.Status);
        Assert.Equal("boom", result.ErrorMessage);
        // Form preserved — still has the earlier baseline.
        Assert.Equal("baseline", form.Title);
        Assert.True(form.HasLiveBroadcast);
    }

    [Fact]
    public async Task Unauthorized_FlowsToReauthOrchestrator()
    {
        // FakeReauth rewrites UnauthorizedException to OperationCanceledException
        // to simulate "reauth cancelled". The coordinator treats OCE as a
        // dirty-guard-style cancel (per §6.7 step 4 UX).
        var client = new FakeYouTubeClient { Throw = new UnauthorizedException("401") };
        var form = new StreamFormViewModel();
        var reauth = new FakeReauth { BehaviorForUnauthorized = FakeReauth.Behavior.ThrowCancel };
        var c = NewCoordinator(client, form, reauth: reauth);

        var result = await c.FetchAsync(allowOverwrite: false, CancellationToken.None);

        Assert.Equal(StreamFetchOutcome.Cancelled, result.Outcome);
    }

    [Fact]
    public async Task ReauthOptions_DirtyFormGuardOnRetry_MatchesAllowOverwrite()
    {
        var client = new FakeYouTubeClient { Response = NewSnapshot() };
        var reauth = new FakeReauth();
        var c = NewCoordinator(client, new StreamFormViewModel(), reauth: reauth);

        await c.FetchAsync(allowOverwrite: false, CancellationToken.None);
        Assert.True(reauth.LastOptions?.DirtyFormGuardOnRetry);

        await c.FetchAsync(allowOverwrite: true, CancellationToken.None);
        Assert.False(reauth.LastOptions?.DirtyFormGuardOnRetry);
    }

    // ---- helpers ----

    private static StreamFormViewModel DirtyForm()
    {
        var f = new StreamFormViewModel();
        f.SetLiveBaseline(f.CaptureSnapshot());
        f.Title = "dirty edit";
        Assert.True(f.IsDirtyVsLive);
        return f;
    }

    private static BroadcastSnapshot NewSnapshot() => new()
    {
        BroadcastId = "bc",
        VideoId = "vid",
        Title = "Title from API",
        Description = "Desc",
        ThumbnailUrl = "https://img/x.jpg",
    };

    private static StreamFetchCoordinator NewCoordinator(
        FakeYouTubeClient client,
        StreamFormViewModel form,
        StubDirtyFormGuard? guard = null,
        FakeReauth? reauth = null)
    {
        return new StreamFetchCoordinator(
            client,
            reauth ?? new FakeReauth(),
            guard ?? new StubDirtyFormGuard { Response = true },
            form,
            NullLogger<StreamFetchCoordinator>.Instance);
    }

    private sealed class FakeYouTubeClient : IYouTubeClient
    {
        public BroadcastSnapshot? Response { get; set; }
        public Exception? Throw { get; set; }
        public int CallCount { get; private set; }

        public Task<BroadcastSnapshot?> GetActiveBroadcastAsync(CancellationToken ct)
        {
            CallCount++;
            if (Throw is not null) throw Throw;
            return Task.FromResult(Response);
        }

        // Slice 5 added write methods to IYouTubeClient. The fetch
        // coordinator never calls them; throwing keeps accidental calls
        // loud instead of silently passing.
        public Task UpdateBroadcastAsync(BroadcastUpdate update, CancellationToken ct) =>
            throw new NotSupportedException("Fetch tests' fake does not implement writes.");

        public Task UpdateVideoAsync(VideoUpdate update, CancellationToken ct) =>
            throw new NotSupportedException("Fetch tests' fake does not implement writes.");
    }

    private sealed class StubDirtyFormGuard : IDirtyFormGuard
    {
        public bool Response { get; set; }
        public int Invocations { get; private set; }

        public Task<bool> ConfirmOverwriteAsync(CancellationToken ct)
        {
            Invocations++;
            return Task.FromResult(Response);
        }
    }

    private sealed class FakeReauth : IReauthOrchestrator
    {
        public enum Behavior { Passthrough, ThrowCancel }

        public Behavior BehaviorForUnauthorized { get; set; } = Behavior.Passthrough;
        public ReauthOperationOptions? LastOptions { get; private set; }

        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            ReauthOperationOptions options,
            CancellationToken ct)
        {
            LastOptions = options;
            try
            {
                return await operation(ct);
            }
            catch (UnauthorizedException)
            {
                if (BehaviorForUnauthorized == Behavior.ThrowCancel)
                {
                    throw new OperationCanceledException("reauth cancelled");
                }
                throw;
            }
        }

        public async Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            ReauthOperationOptions options,
            CancellationToken ct)
        {
            await ExecuteAsync<object?>(async t => { await operation(t); return null; }, options, ct);
        }
    }
}
