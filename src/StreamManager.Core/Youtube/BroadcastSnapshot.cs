namespace StreamManager.Core.Youtube;

// Read-only snapshot of the user's currently-active broadcast, combining
// liveBroadcasts.list + the underlying videos.list + the current thumbnail URL.
// This is the shape the coordinator (and later the Apply pipeline) deals with,
// kept free of any Avalonia / ViewModel types so Core stays UI-agnostic.
//
// Fields mirror design.md §5 exactly so the mapping to StreamFormSnapshot is
// a 1:1 copy; deviations from API defaults (e.g. LatencyPreference fallback)
// are resolved in BroadcastSnapshotMapper.
public sealed record BroadcastSnapshot
{
    public string BroadcastId { get; init; } = "";
    public string VideoId { get; init; } = "";

    // --- liveBroadcasts.update → snippet ---
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public DateTimeOffset? ScheduledStartTime { get; init; }
    public DateTimeOffset? ScheduledEndTime { get; init; }

    // --- liveBroadcasts.update → status ---
    public string PrivacyStatus { get; init; } = "public";
    public bool SelfDeclaredMadeForKids { get; init; }

    // --- liveBroadcasts.update → contentDetails ---
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

    // --- videos.update → snippet ---
    public string? CategoryId { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string? DefaultLanguage { get; init; }
    public string? DefaultAudioLanguage { get; init; }

    // --- thumbnail URL served by YouTube (read-only preview until user picks a file) ---
    public string? ThumbnailUrl { get; init; }
}
