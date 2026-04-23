namespace StreamManager.Core.Youtube;

// Payload for liveBroadcasts.update (design.md §4 + §5). Built from the
// current form state by ApplyOrchestrator and consumed by IYouTubeClient.
// Mirrors BroadcastSnapshot's broadcast-resource fields exactly so the
// snapshot→update mapping is a 1:1 copy plus the BroadcastId target.
public sealed record BroadcastUpdate
{
    public string BroadcastId { get; init; } = "";

    // snippet
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public DateTimeOffset? ScheduledStartTime { get; init; }
    public DateTimeOffset? ScheduledEndTime { get; init; }

    // status
    public string PrivacyStatus { get; init; } = "public";
    public bool SelfDeclaredMadeForKids { get; init; }

    // contentDetails
    public bool EnableAutoStart { get; init; } = true;
    public bool EnableAutoStop { get; init; } = true;
    public bool EnableClosedCaptions { get; init; }
    public bool EnableDvr { get; init; } = true;
    public bool EnableEmbed { get; init; } = true;
    public bool RecordFromStart { get; init; } = true;
    public bool StartWithSlate { get; init; }
    public bool EnableContentEncryption { get; init; }
    public bool EnableLowLatency { get; init; }
    public string LatencyPreference { get; init; } = "normal";
    public bool EnableMonitorStream { get; init; } = true;
    public int BroadcastStreamDelayMs { get; init; }
    public string Projection { get; init; } = "rectangular";
    public string StereoLayout { get; init; } = "mono";
    public string ClosedCaptionsType { get; init; } = "closedCaptionsDisabled";
}

// Payload for videos.update (design.md §4 + §5). Smaller than BroadcastUpdate
// because most fields live on the broadcast resource; the video resource owns
// categoryId + tags + language hints. Title is included because part=snippet
// is a full-snippet replace on the API side and the YouTube API requires
// snippet.title to be present (it stays in sync with the broadcast title).
public sealed record VideoUpdate
{
    public string VideoId { get; init; } = "";
    public string Title { get; init; } = "";
    public string? CategoryId { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string? DefaultLanguage { get; init; }
    public string? DefaultAudioLanguage { get; init; }
}
