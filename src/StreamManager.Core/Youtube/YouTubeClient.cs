using System.Net;
using Google.Apis.Services;
using Google.Apis.Util;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using StreamManager.Core.Auth;

namespace StreamManager.Core.Youtube;

// Concrete IYouTubeClient backed by the Google.Apis.YouTube.v3 SDK. Uses the
// current access token from IAuthState per request (retrieved at call time so
// silent reconnects in IYouTubeAuthenticator stay visible here). 401 responses
// are rethrown as UnauthorizedException so IReauthOrchestrator drives the
// §6.7 reconnect modal; other GoogleApiExceptions surface as-is for the
// coordinator to translate to the amber "fetch failed" banner (§6.2).
public sealed class YouTubeClient : IYouTubeClient, IDisposable
{
    private const string ApplicationName = "StreamManager";

    private readonly IAuthState _authState;
    private readonly YouTubeService _service;
    private readonly ILogger<YouTubeClient> _log;

    public YouTubeClient(IAuthState authState, ILogger<YouTubeClient> log)
    {
        _authState = authState;
        _log = log;
        _service = new YouTubeService(new BaseClientService.Initializer
        {
            ApplicationName = ApplicationName,
        });
    }

    public async Task<BroadcastSnapshot?> GetActiveBroadcastAsync(CancellationToken ct)
    {
        var token = RequireAccessToken();

        try
        {
            var broadcastReq = _service.LiveBroadcasts.List(
                new Repeatable<string>(new[] { "snippet", "status", "contentDetails" }));
            broadcastReq.Mine = true;
            broadcastReq.BroadcastStatus = LiveBroadcastsResource.ListRequest.BroadcastStatusEnum.Active;
            broadcastReq.MaxResults = 1;
            broadcastReq.OauthToken = token;

            var broadcastResp = await broadcastReq.ExecuteAsync(ct).ConfigureAwait(false);
            var broadcast = broadcastResp?.Items?.FirstOrDefault();
            if (broadcast is null)
            {
                return null;
            }

            var videoReq = _service.Videos.List(
                new Repeatable<string>(new[] { "snippet", "contentDetails", "status", "localizations" }));
            videoReq.Id = new Repeatable<string>(new[] { broadcast.Id });
            videoReq.OauthToken = token;

            var videoResp = await videoReq.ExecuteAsync(ct).ConfigureAwait(false);
            var video = videoResp?.Items?.FirstOrDefault();
            if (video is null)
            {
                // Broadcast came back but no matching video — unexpected; surface as
                // "not live" rather than crashing. Log so we notice the drift.
                _log.LogWarning(
                    "liveBroadcasts.list returned {Id} but videos.list found no match",
                    broadcast.Id);
                return null;
            }

            return BroadcastSnapshotMapper.Map(broadcast, video);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedException("YouTube API returned 401.", ex);
        }
    }

    public async Task UpdateBroadcastAsync(BroadcastUpdate update, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(update);
        var token = RequireAccessToken();

        var resource = new LiveBroadcast
        {
            Id = update.BroadcastId,
            Snippet = new LiveBroadcastSnippet
            {
                Title = update.Title,
                Description = update.Description,
                ScheduledStartTimeDateTimeOffset = update.ScheduledStartTime,
                ScheduledEndTimeDateTimeOffset = update.ScheduledEndTime,
            },
            Status = new LiveBroadcastStatus
            {
                PrivacyStatus = update.PrivacyStatus,
                SelfDeclaredMadeForKids = update.SelfDeclaredMadeForKids,
            },
            ContentDetails = new LiveBroadcastContentDetails
            {
                EnableAutoStart = update.EnableAutoStart,
                EnableAutoStop = update.EnableAutoStop,
                EnableClosedCaptions = update.EnableClosedCaptions,
                EnableDvr = update.EnableDvr,
                EnableEmbed = update.EnableEmbed,
                RecordFromStart = update.RecordFromStart,
                StartWithSlate = update.StartWithSlate,
                EnableContentEncryption = update.EnableContentEncryption,
                EnableLowLatency = update.EnableLowLatency,
                LatencyPreference = update.LatencyPreference,
                Projection = update.Projection,
                StereoLayout = update.StereoLayout,
                ClosedCaptionsType = update.ClosedCaptionsType,
                MonitorStream = new MonitorStreamInfo
                {
                    EnableMonitorStream = update.EnableMonitorStream,
                    BroadcastStreamDelayMs = update.BroadcastStreamDelayMs,
                },
            },
        };

        try
        {
            var req = _service.LiveBroadcasts.Update(
                resource,
                new Repeatable<string>(new[] { "snippet", "status", "contentDetails" }));
            req.OauthToken = token;
            await req.ExecuteAsync(ct).ConfigureAwait(false);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedException("YouTube API returned 401.", ex);
        }
    }

    public async Task UpdateVideoAsync(VideoUpdate update, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(update);
        var token = RequireAccessToken();

        var resource = new Video
        {
            Id = update.VideoId,
            Snippet = new VideoSnippet
            {
                // CategoryId is required on a Video.snippet write — keep it as
                // whatever the form holds (mapper falls back to last-fetched
                // value if user didn't change it).
                CategoryId = update.CategoryId,
                Tags = update.Tags.ToList(),
                DefaultLanguage = update.DefaultLanguage,
                DefaultAudioLanguage = update.DefaultAudioLanguage,
                // Title is required on a snippet write — kept in sync with
                // the broadcast title (the orchestrator pulls it from the
                // same form field).
                Title = update.Title,
            },
        };

        try
        {
            var req = _service.Videos.Update(
                resource,
                new Repeatable<string>(new[] { "snippet" }));
            req.OauthToken = token;
            await req.ExecuteAsync(ct).ConfigureAwait(false);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedException("YouTube API returned 401.", ex);
        }
    }

    private string RequireAccessToken()
    {
        var token = _authState.AccessToken;
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException(
                "Cannot call YouTube API: no access token available (not connected).");
        }
        return token;
    }

    public void Dispose() => _service.Dispose();
}
