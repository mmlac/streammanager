namespace StreamManager.App.ViewModels;

public sealed record StreamFormSnapshot
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string? CategoryId { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string PrivacyStatus { get; init; } = StreamFormEnums.PrivacyStatuses.Public;
    public bool SelfDeclaredMadeForKids { get; init; }
    public bool EnableAutoStart { get; init; } = true;
    public bool EnableAutoStop { get; init; } = true;
    public bool EnableClosedCaptions { get; init; }
    public bool EnableDvr { get; init; } = true;
    public bool EnableEmbed { get; init; } = true;
    public bool RecordFromStart { get; init; } = true;
    public bool StartWithSlate { get; init; }
    public bool EnableContentEncryption { get; init; }
    public bool EnableLowLatency { get; init; }
    public string LatencyPreference { get; init; } = StreamFormEnums.LatencyPreferences.Normal;
    public bool EnableMonitorStream { get; init; } = true;
    public int BroadcastStreamDelayMs { get; init; }
    public string Projection { get; init; } = StreamFormEnums.Projections.Rectangular;
    public string StereoLayout { get; init; } = StreamFormEnums.StereoLayouts.Mono;
    public string ClosedCaptionsType { get; init; } = StreamFormEnums.ClosedCaptionsTypes.Disabled;
    public DateTimeOffset? ScheduledStartTime { get; init; }
    public DateTimeOffset? ScheduledEndTime { get; init; }
    public string? DefaultLanguage { get; init; }
    public string? DefaultAudioLanguage { get; init; }
    public string? ThumbnailPath { get; init; }

    public bool ValueEquals(StreamFormSnapshot other)
    {
        return Title == other.Title
            && Description == other.Description
            && CategoryId == other.CategoryId
            && TagListEquals(Tags, other.Tags)
            && PrivacyStatus == other.PrivacyStatus
            && SelfDeclaredMadeForKids == other.SelfDeclaredMadeForKids
            && EnableAutoStart == other.EnableAutoStart
            && EnableAutoStop == other.EnableAutoStop
            && EnableClosedCaptions == other.EnableClosedCaptions
            && EnableDvr == other.EnableDvr
            && EnableEmbed == other.EnableEmbed
            && RecordFromStart == other.RecordFromStart
            && StartWithSlate == other.StartWithSlate
            && EnableContentEncryption == other.EnableContentEncryption
            && EnableLowLatency == other.EnableLowLatency
            && LatencyPreference == other.LatencyPreference
            && EnableMonitorStream == other.EnableMonitorStream
            && BroadcastStreamDelayMs == other.BroadcastStreamDelayMs
            && Projection == other.Projection
            && StereoLayout == other.StereoLayout
            && ClosedCaptionsType == other.ClosedCaptionsType
            && ScheduledStartTime == other.ScheduledStartTime
            && ScheduledEndTime == other.ScheduledEndTime
            && DefaultLanguage == other.DefaultLanguage
            && DefaultAudioLanguage == other.DefaultAudioLanguage
            && ThumbnailPath == other.ThumbnailPath;
    }

    private static bool TagListEquals(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
}

public sealed record PresetLineage(string Id, string Name);
