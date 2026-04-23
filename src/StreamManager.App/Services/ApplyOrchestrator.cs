using Microsoft.Extensions.Logging;
using StreamManager.App.ViewModels;
using StreamManager.Core.Auth;
using StreamManager.Core.Youtube;

namespace StreamManager.App.Services;

// §6.6 implementation. Orchestrates the Apply pipeline:
//   1. Pre-flight: check thumbnailPath reachability if set; on miss, prompt
//      the user (Apply-without-thumbnail / Cancel).
//   2. liveBroadcasts.update with snippet + status + contentDetails.
//   3. videos.update with categoryId + tags + languages.
//   4. thumbnails.set if the form's thumbnailPath changed since last fetch
//      AND is reachable. (Slice 8 wires the real upload; slice 5 has a stub.)
//   5. Re-fetch via the slice-4 coordinator with allowOverwrite=true so the
//      dirty-form prompt is suppressed (the user just pushed the edits).
//   6. On any failure surface the failing step in ApplyResult; no rollback.
//
// Reauth (§6.7) is honored by routing each API call through IReauthOrchestrator
// with DirtyFormGuardOnRetry=false: an Apply-time reauth is itself a user-driven
// write, not a refresh, so the §6.2 dirty-form check doesn't apply.
//
// The orchestrator also enforces a re-entrancy guard so a queued Apply click
// can't kick a second pipeline while one is in flight (§ test list "Apply
// pressed while orchestrator is already running is a no-op").
public sealed class ApplyOrchestrator : IApplyOrchestrator
{
    private readonly StreamFormViewModel _form;
    private readonly IYouTubeClient _client;
    private readonly IThumbnailUploader _thumbnailUploader;
    private readonly IThumbnailFileChecker _thumbnailChecker;
    private readonly IUnreachableThumbnailPrompt _unreachablePrompt;
    private readonly IReauthOrchestrator _reauth;
    private readonly IStreamFetchCoordinator _fetchCoordinator;
    private readonly ILogger<ApplyOrchestrator> _log;

    private int _running;

    public ApplyOrchestrator(
        StreamFormViewModel form,
        IYouTubeClient client,
        IThumbnailUploader thumbnailUploader,
        IThumbnailFileChecker thumbnailChecker,
        IUnreachableThumbnailPrompt unreachablePrompt,
        IReauthOrchestrator reauth,
        IStreamFetchCoordinator fetchCoordinator,
        ILogger<ApplyOrchestrator> log)
    {
        _form = form;
        _client = client;
        _thumbnailUploader = thumbnailUploader;
        _thumbnailChecker = thumbnailChecker;
        _unreachablePrompt = unreachablePrompt;
        _reauth = reauth;
        _fetchCoordinator = fetchCoordinator;
        _log = log;
    }

    public async Task<ApplyResult> ApplyAsync(CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            // Already running — silently no-op so the queued click doesn't
            // double-submit. UI also disables the button while running, but
            // this defends against bypass.
            _log.LogDebug("Apply ignored: orchestrator already running");
            return new ApplyResult(ApplyOutcome.Cancelled, ErrorMessage: "Apply already in progress.");
        }

        try
        {
            return await RunAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private async Task<ApplyResult> RunAsync(CancellationToken ct)
    {
        // Defensive validation gate (button-level guard is the primary).
        // Without the IDs we have no broadcast/video to write to.
        if (_form.HasErrors)
        {
            return new ApplyResult(
                ApplyOutcome.Failed,
                ApplyStep.None,
                "Form has validation errors.");
        }
        var broadcastId = _form.LastFetchedBroadcastId;
        var videoId = _form.LastFetchedVideoId;
        if (string.IsNullOrEmpty(broadcastId) || string.IsNullOrEmpty(videoId))
        {
            return new ApplyResult(
                ApplyOutcome.Failed,
                ApplyStep.None,
                "No active broadcast loaded — fetch one first.");
        }

        var snapshot = _form.CaptureSnapshot();
        var thumbnailChanged = _form.IsThumbnailChangedFromLive;

        // Step 1: pre-flight thumbnail reachability.
        var thumbnailReachable = false;
        if (!string.IsNullOrEmpty(snapshot.ThumbnailPath))
        {
            thumbnailReachable = _thumbnailChecker.IsReachable(snapshot.ThumbnailPath);
            if (!thumbnailReachable)
            {
                var decision = await _unreachablePrompt
                    .PromptAsync(snapshot.ThumbnailPath, ct)
                    .ConfigureAwait(false);
                if (decision == UnreachableThumbnailDecision.Cancel)
                {
                    _log.LogInformation("Apply cancelled by user (unreachable thumbnail)");
                    return new ApplyResult(
                        ApplyOutcome.Cancelled,
                        ErrorMessage: "Cancelled by user.");
                }
                // ApplyWithoutThumbnail: proceed; thumbnails.set will be skipped.
            }
        }

        // Step 2: liveBroadcasts.update.
        var broadcastUpdate = BuildBroadcastUpdate(broadcastId, snapshot);
        try
        {
            await _reauth.ExecuteAsync(
                token => _client.UpdateBroadcastAsync(broadcastUpdate, token),
                new ReauthOperationOptions { DirtyFormGuardOnRetry = false },
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _log.LogInformation(ex, "Apply cancelled at step 1 (reauth declined)");
            return new ApplyResult(
                ApplyOutcome.Cancelled,
                ApplyStep.UpdateBroadcast,
                "Cancelled by user.");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Apply failed at step 1 (liveBroadcasts.update)");
            return new ApplyResult(
                ApplyOutcome.Failed,
                ApplyStep.UpdateBroadcast,
                $"Step 1 failed (update broadcast): {ex.Message}");
        }

        // Step 3: videos.update.
        var videoUpdate = BuildVideoUpdate(videoId, snapshot);
        try
        {
            await _reauth.ExecuteAsync(
                token => _client.UpdateVideoAsync(videoUpdate, token),
                new ReauthOperationOptions { DirtyFormGuardOnRetry = false },
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _log.LogInformation(ex, "Apply cancelled at step 2 (reauth declined after broadcast update)");
            return new ApplyResult(
                ApplyOutcome.Cancelled,
                ApplyStep.UpdateVideo,
                "Cancelled by user.",
                BroadcastUpdated: true);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Apply failed at step 2 (videos.update)");
            return new ApplyResult(
                ApplyOutcome.Failed,
                ApplyStep.UpdateVideo,
                $"Step 2 failed (update video): {ex.Message} " +
                "(broadcast-level update did succeed)",
                BroadcastUpdated: true);
        }

        // Step 4: thumbnails.set (only when changed AND reachable).
        var didThumbnailUpload = false;
        if (thumbnailChanged
            && !string.IsNullOrEmpty(snapshot.ThumbnailPath)
            && thumbnailReachable)
        {
            try
            {
                await _reauth.ExecuteAsync(
                    token => _thumbnailUploader.SetThumbnailAsync(videoId, snapshot.ThumbnailPath, token),
                    new ReauthOperationOptions { DirtyFormGuardOnRetry = false },
                    ct).ConfigureAwait(false);
                didThumbnailUpload = true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Apply failed at step 3 (thumbnails.set)");
                // Step 4 failed — but design says re-fetch still runs so the
                // user can see YouTube's current state. The result reports
                // the failure; the re-fetch happens below before returning.
                await TryRefetchAsync(ct).ConfigureAwait(false);
                return new ApplyResult(
                    ApplyOutcome.Failed,
                    ApplyStep.UpdateThumbnail,
                    $"Step 3 failed (set thumbnail): {ex.Message}",
                    BroadcastUpdated: true);
            }
        }

        // Step 5: re-fetch with allowOverwrite=true so the user sees what
        // YouTube actually applied and the form's IsDirtyVsLive resets.
        await TryRefetchAsync(ct).ConfigureAwait(false);

        _log.LogInformation(
            "Apply succeeded (broadcast={Broadcast}, video={Video}, thumbnailUpload={Thumb})",
            broadcastId, videoId, didThumbnailUpload);

        return new ApplyResult(ApplyOutcome.Success, BroadcastUpdated: true);
    }

    private async Task TryRefetchAsync(CancellationToken ct)
    {
        try
        {
            await _fetchCoordinator.FetchAsync(allowOverwrite: true, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Re-fetch is best-effort — Apply itself succeeded. Surface to
            // the log; the live indicator will go amber via the coordinator's
            // own error path if it threw before it could update state.
            _log.LogWarning(ex, "Post-Apply re-fetch threw");
        }
    }

    internal static BroadcastUpdate BuildBroadcastUpdate(string broadcastId, StreamFormSnapshot s) => new()
    {
        BroadcastId = broadcastId,
        Title = s.Title,
        Description = s.Description,
        ScheduledStartTime = s.ScheduledStartTime,
        ScheduledEndTime = s.ScheduledEndTime,
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
    };

    internal static VideoUpdate BuildVideoUpdate(string videoId, StreamFormSnapshot s) => new()
    {
        VideoId = videoId,
        Title = s.Title,
        CategoryId = s.CategoryId,
        Tags = s.Tags,
        DefaultLanguage = s.DefaultLanguage,
        DefaultAudioLanguage = s.DefaultAudioLanguage,
    };
}
