using Microsoft.Extensions.Logging;
using StreamManager.App.ViewModels;
using StreamManager.Core.Auth;
using StreamManager.Core.Youtube;

namespace StreamManager.App.Services;

// Slice 4 wiring. Responsibilities:
//
//   1. Unless allowOverwrite, invoke IDirtyFormGuard before a fetch clobbers
//      unsaved edits (§6.2 step 2).
//   2. Run the YouTube call through IReauthOrchestrator so 401s drive the
//      §6.7 reconnect modal, with DirtyFormGuardOnRetry matching the trigger.
//   3. On success, feed the mapped snapshot to StreamFormViewModel.SetLiveBaseline
//      (which resets IsDirtyVsLive and copies values). HasLiveBroadcast is
//      flipped to reflect whether an active broadcast was found.
//   4. On exception, preserve the existing form state (design: "amber =
//      last known form state preserved") and surface the error message.
public sealed class StreamFetchCoordinator : IStreamFetchCoordinator
{
    private readonly IYouTubeClient _client;
    private readonly IReauthOrchestrator _reauth;
    private readonly IDirtyFormGuard _dirtyFormGuard;
    private readonly StreamFormViewModel _form;
    private readonly ILogger<StreamFetchCoordinator> _log;

    public StreamFetchCoordinator(
        IYouTubeClient client,
        IReauthOrchestrator reauth,
        IDirtyFormGuard dirtyFormGuard,
        StreamFormViewModel form,
        ILogger<StreamFetchCoordinator> log)
    {
        _client = client;
        _reauth = reauth;
        _dirtyFormGuard = dirtyFormGuard;
        _form = form;
        _log = log;
    }

    public async Task<StreamFetchResult> FetchAsync(bool allowOverwrite, CancellationToken ct)
    {
        if (!allowOverwrite)
        {
            var proceed = await _dirtyFormGuard.ConfirmOverwriteAsync(ct).ConfigureAwait(false);
            if (!proceed)
            {
                _log.LogInformation("Fetch aborted: user declined to overwrite dirty form");
                return new StreamFetchResult(
                    StreamFetchOutcome.Cancelled,
                    LiveIndicatorStatus.Unknown);
            }
        }

        // When reauth fires on a re-fetch, §6.7 step 3 says the dirty-form
        // protection must still apply to the retry. allowOverwrite (post-Apply
        // re-fetch) explicitly bypasses that.
        var options = new ReauthOperationOptions
        {
            DirtyFormGuardOnRetry = !allowOverwrite,
        };

        BroadcastSnapshot? snapshot;
        try
        {
            snapshot = await _reauth.ExecuteAsync(
                async token => await _client.GetActiveBroadcastAsync(token).ConfigureAwait(false),
                options,
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // Reauth cancelled by user (§6.7 step 4) or post-reauth dirty-guard
            // declined. Leave form untouched.
            _log.LogInformation(ex, "Fetch cancelled during reauth/guard");
            return new StreamFetchResult(
                StreamFetchOutcome.Cancelled,
                LiveIndicatorStatus.Unknown);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Fetch failed; preserving last-known form state");
            return new StreamFetchResult(
                StreamFetchOutcome.FetchFailed,
                LiveIndicatorStatus.FetchFailed,
                ErrorMessage: ex.Message);
        }

        if (snapshot is null)
        {
            // Connected, no active broadcast. Form untouched — design says
            // "form shows last state ... Apply disabled, Not live indicator".
            _form.HasLiveBroadcast = false;
            _form.RemoteThumbnailUrl = null;
            return new StreamFetchResult(
                StreamFetchOutcome.NotLive,
                LiveIndicatorStatus.NotLive);
        }

        _form.SetLiveBaseline(ToFormSnapshot(snapshot));
        _form.HasLiveBroadcast = true;
        _form.RemoteThumbnailUrl = snapshot.ThumbnailUrl;

        return new StreamFetchResult(
            StreamFetchOutcome.Live,
            LiveIndicatorStatus.Live,
            Snapshot: snapshot);
    }

    private static StreamFormSnapshot ToFormSnapshot(BroadcastSnapshot s) => new()
    {
        Title = s.Title,
        Description = s.Description,
        CategoryId = s.CategoryId,
        Tags = s.Tags,
        PrivacyStatus = s.PrivacyStatus,
        SelfDeclaredMadeForKids = s.SelfDeclaredMadeForKids,
        EnableAutoStart = s.EnableAutoStart,
        EnableAutoStop = s.EnableAutoStop,
        EnableClosedCaptions = s.EnableClosedCaptions,
        EnableDvr = s.EnableDvr,
        EnableEmbed = s.EnableEmbed,
        RecordFromStart = s.RecordFromStart,
        StartWithSlate = s.StartWithSlate,
        EnableContentEncryption = s.EnableContentEncryption,
        EnableLowLatency = s.EnableLowLatency,
        LatencyPreference = s.LatencyPreference,
        EnableMonitorStream = s.EnableMonitorStream,
        BroadcastStreamDelayMs = s.BroadcastStreamDelayMs,
        Projection = s.Projection,
        StereoLayout = s.StereoLayout,
        ClosedCaptionsType = s.ClosedCaptionsType,
        ScheduledStartTime = s.ScheduledStartTime,
        ScheduledEndTime = s.ScheduledEndTime,
        DefaultLanguage = s.DefaultLanguage,
        DefaultAudioLanguage = s.DefaultAudioLanguage,
        // ThumbnailPath is a locally-picked file (slice 8). Fetching from
        // YouTube doesn't populate it — the remote URL lives in
        // StreamFormViewModel.RemoteThumbnailUrl instead.
        ThumbnailPath = null,
    };
}
