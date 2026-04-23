using StreamManager.Core.Presets;

namespace StreamManager.App.ViewModels;

// Bridges the persisted `Preset` (Core) with the editable `StreamFormSnapshot`
// (App layer). The form's value-typed snapshot is intentionally independent
// from the persistence record so the two layers can evolve separately —
// this mapper is the single place that has to change when either side adds
// a field.
public static class PresetMapper
{
    public static StreamFormSnapshot ToSnapshot(Preset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return new StreamFormSnapshot
        {
            Title = preset.Title,
            Description = preset.Description,
            CategoryId = preset.CategoryId,
            Tags = preset.Tags.ToArray(),
            PrivacyStatus = preset.PrivacyStatus,
            SelfDeclaredMadeForKids = preset.SelfDeclaredMadeForKids,
            EnableAutoStart = preset.EnableAutoStart,
            EnableAutoStop = preset.EnableAutoStop,
            EnableClosedCaptions = preset.EnableClosedCaptions,
            EnableDvr = preset.EnableDvr,
            EnableEmbed = preset.EnableEmbed,
            RecordFromStart = preset.RecordFromStart,
            StartWithSlate = preset.StartWithSlate,
            EnableContentEncryption = preset.EnableContentEncryption,
            EnableLowLatency = preset.EnableLowLatency,
            LatencyPreference = preset.LatencyPreference,
            EnableMonitorStream = preset.EnableMonitorStream,
            BroadcastStreamDelayMs = preset.BroadcastStreamDelayMs,
            Projection = preset.Projection,
            StereoLayout = preset.StereoLayout,
            ClosedCaptionsType = preset.ClosedCaptionsType,
            ScheduledStartTime = preset.ScheduledStartTime,
            ScheduledEndTime = preset.ScheduledEndTime,
            DefaultLanguage = preset.DefaultLanguage,
            DefaultAudioLanguage = preset.DefaultAudioLanguage,
            ThumbnailPath = preset.ThumbnailPath,
        };
    }

    public static Preset ToPreset(
        StreamFormSnapshot snapshot,
        string id,
        string name,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(name);
        return new Preset
        {
            Id = id,
            Name = name,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Title = snapshot.Title,
            Description = snapshot.Description,
            CategoryId = snapshot.CategoryId,
            Tags = snapshot.Tags.ToArray(),
            PrivacyStatus = snapshot.PrivacyStatus,
            SelfDeclaredMadeForKids = snapshot.SelfDeclaredMadeForKids,
            EnableAutoStart = snapshot.EnableAutoStart,
            EnableAutoStop = snapshot.EnableAutoStop,
            EnableClosedCaptions = snapshot.EnableClosedCaptions,
            EnableDvr = snapshot.EnableDvr,
            EnableEmbed = snapshot.EnableEmbed,
            RecordFromStart = snapshot.RecordFromStart,
            StartWithSlate = snapshot.StartWithSlate,
            EnableContentEncryption = snapshot.EnableContentEncryption,
            EnableLowLatency = snapshot.EnableLowLatency,
            LatencyPreference = snapshot.LatencyPreference,
            EnableMonitorStream = snapshot.EnableMonitorStream,
            BroadcastStreamDelayMs = snapshot.BroadcastStreamDelayMs,
            Projection = snapshot.Projection,
            StereoLayout = snapshot.StereoLayout,
            ClosedCaptionsType = snapshot.ClosedCaptionsType,
            ScheduledStartTime = snapshot.ScheduledStartTime,
            ScheduledEndTime = snapshot.ScheduledEndTime,
            DefaultLanguage = snapshot.DefaultLanguage,
            DefaultAudioLanguage = snapshot.DefaultAudioLanguage,
            ThumbnailPath = snapshot.ThumbnailPath,
        };
    }
}
