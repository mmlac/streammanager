using Google.Apis.YouTube.v3.Data;

namespace StreamManager.Core.Youtube;

// Pure mapping from raw YouTube API resources (LiveBroadcast + Video) to the
// Core BroadcastSnapshot. Isolated from the API client so it's trivially
// testable with hand-constructed data objects (no HTTP mocks needed).
//
// Defaults follow design.md §5; null API fields resolve to the same defaults
// BroadcastSnapshot ships with, so "missing contentDetails" etc. doesn't
// crash.
public static class BroadcastSnapshotMapper
{
    public static BroadcastSnapshot Map(LiveBroadcast broadcast, Video video)
    {
        ArgumentNullException.ThrowIfNull(broadcast);
        ArgumentNullException.ThrowIfNull(video);

        var snippet = broadcast.Snippet;
        var status = broadcast.Status;
        var cd = broadcast.ContentDetails;
        var monitor = cd?.MonitorStream;
        var videoSnippet = video.Snippet;

        return new BroadcastSnapshot
        {
            BroadcastId = broadcast.Id ?? "",
            VideoId = video.Id ?? "",

            // snippet
            Title = snippet?.Title ?? "",
            Description = snippet?.Description ?? "",
            ScheduledStartTime = snippet?.ScheduledStartTimeDateTimeOffset,
            ScheduledEndTime = snippet?.ScheduledEndTimeDateTimeOffset,

            // status
            PrivacyStatus = NonEmptyOr(status?.PrivacyStatus, "public"),
            SelfDeclaredMadeForKids = status?.SelfDeclaredMadeForKids ?? status?.MadeForKids ?? false,

            // contentDetails
            EnableAutoStart = cd?.EnableAutoStart ?? true,
            EnableAutoStop = cd?.EnableAutoStop ?? true,
            EnableClosedCaptions = cd?.EnableClosedCaptions ?? false,
            EnableDvr = cd?.EnableDvr ?? true,
            EnableEmbed = cd?.EnableEmbed ?? true,
            RecordFromStart = cd?.RecordFromStart ?? true,
            StartWithSlate = cd?.StartWithSlate ?? false,
            EnableContentEncryption = cd?.EnableContentEncryption ?? false,
            EnableLowLatency = cd?.EnableLowLatency ?? false,
            LatencyPreference = NonEmptyOr(cd?.LatencyPreference, "normal"),
            EnableMonitorStream = monitor?.EnableMonitorStream ?? true,
            BroadcastStreamDelayMs = SafeToInt(monitor?.BroadcastStreamDelayMs),
            Projection = NonEmptyOr(cd?.Projection, "rectangular"),
            StereoLayout = NonEmptyOr(cd?.StereoLayout, "mono"),
            ClosedCaptionsType = NonEmptyOr(cd?.ClosedCaptionsType, "closedCaptionsDisabled"),

            // video snippet
            CategoryId = NullIfEmpty(videoSnippet?.CategoryId),
            Tags = videoSnippet?.Tags is { } tags ? tags.ToArray() : Array.Empty<string>(),
            DefaultLanguage = NullIfEmpty(videoSnippet?.DefaultLanguage),
            DefaultAudioLanguage = NullIfEmpty(videoSnippet?.DefaultAudioLanguage),

            // thumbnail — prefer highest available resolution the broadcast exposes.
            // Falls back to the underlying video's thumbnails if the broadcast
            // resource didn't include one.
            ThumbnailUrl = PickThumbnailUrl(snippet?.Thumbnails) ?? PickThumbnailUrl(videoSnippet?.Thumbnails),
        };
    }

    private static string NonEmptyOr(string? value, string fallback) =>
        string.IsNullOrEmpty(value) ? fallback : value;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;

    private static int SafeToInt(long? value)
    {
        if (value is null) return 0;
        var v = value.Value;
        if (v <= 0) return 0;
        return v > int.MaxValue ? int.MaxValue : (int)v;
    }

    private static string? PickThumbnailUrl(ThumbnailDetails? details)
    {
        if (details is null) return null;
        return details.Maxres?.Url
            ?? details.Standard?.Url
            ?? details.High?.Url
            ?? details.Medium?.Url
            ?? details.Default__?.Url;
    }
}
