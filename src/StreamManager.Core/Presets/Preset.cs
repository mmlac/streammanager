namespace StreamManager.Core.Presets;

// On-disk shape of a single preset entry in `presets.json`, mirroring
// design.md §5 field-for-field. The record is a dumb value container; the
// mapping between a Preset and the editable form lives in the App layer.
public sealed record Preset
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    // liveBroadcasts.update → snippet
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public DateTimeOffset? ScheduledStartTime { get; init; }
    public DateTimeOffset? ScheduledEndTime { get; init; }

    // liveBroadcasts.update → status
    public string PrivacyStatus { get; init; } = "public";
    public bool SelfDeclaredMadeForKids { get; init; }

    // liveBroadcasts.update → contentDetails
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

    // videos.update → snippet
    public string? CategoryId { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string? DefaultLanguage { get; init; }
    public string? DefaultAudioLanguage { get; init; }

    // thumbnails.set — absolute path, referenced in place; null = don't touch.
    public string? ThumbnailPath { get; init; }
}
