using StreamManager.App.ViewModels;
using StreamManager.Core.Presets;
using Xunit;

namespace StreamManager.Core.Tests.Presets;

public class PresetMapperTests
{
    [Fact]
    public void ToSnapshot_CopiesAllFields()
    {
        var now = DateTimeOffset.UtcNow;
        var preset = new Preset
        {
            Id = "id",
            Name = "name",
            CreatedAt = now,
            UpdatedAt = now,
            Title = "t",
            Description = "d",
            ScheduledStartTime = now.AddHours(1),
            ScheduledEndTime = now.AddHours(2),
            PrivacyStatus = "private",
            SelfDeclaredMadeForKids = true,
            EnableAutoStart = false,
            EnableAutoStop = false,
            EnableClosedCaptions = true,
            EnableDvr = false,
            EnableEmbed = false,
            RecordFromStart = false,
            StartWithSlate = true,
            EnableContentEncryption = true,
            EnableLowLatency = true,
            LatencyPreference = "low",
            EnableMonitorStream = false,
            BroadcastStreamDelayMs = 1500,
            Projection = "mesh",
            StereoLayout = "top_bottom",
            ClosedCaptionsType = "closedCaptionsEmbedInVideo",
            CategoryId = "24",
            Tags = new[] { "a", "b" },
            DefaultLanguage = "en",
            DefaultAudioLanguage = "de",
            ThumbnailPath = "/tmp/x.png",
        };

        var snap = PresetMapper.ToSnapshot(preset);

        Assert.Equal("t", snap.Title);
        Assert.Equal("d", snap.Description);
        Assert.Equal(now.AddHours(1), snap.ScheduledStartTime);
        Assert.Equal(now.AddHours(2), snap.ScheduledEndTime);
        Assert.Equal("private", snap.PrivacyStatus);
        Assert.True(snap.SelfDeclaredMadeForKids);
        Assert.False(snap.EnableAutoStart);
        Assert.True(snap.EnableClosedCaptions);
        Assert.False(snap.EnableDvr);
        Assert.Equal("low", snap.LatencyPreference);
        Assert.Equal(1500, snap.BroadcastStreamDelayMs);
        Assert.Equal("mesh", snap.Projection);
        Assert.Equal("top_bottom", snap.StereoLayout);
        Assert.Equal("closedCaptionsEmbedInVideo", snap.ClosedCaptionsType);
        Assert.Equal("24", snap.CategoryId);
        Assert.Equal(new[] { "a", "b" }, snap.Tags.ToArray());
        Assert.Equal("en", snap.DefaultLanguage);
        Assert.Equal("de", snap.DefaultAudioLanguage);
        Assert.Equal("/tmp/x.png", snap.ThumbnailPath);
    }

    [Fact]
    public void ToPreset_CopiesAllFields_AndPreservesMetadata()
    {
        var created = new DateTimeOffset(2026, 01, 01, 0, 0, 0, TimeSpan.Zero);
        var updated = new DateTimeOffset(2026, 04, 23, 0, 0, 0, TimeSpan.Zero);
        var snap = new StreamFormSnapshot
        {
            Title = "t",
            Description = "d",
            CategoryId = "20",
            Tags = new[] { "one" },
            PrivacyStatus = "public",
            BroadcastStreamDelayMs = 42,
            ThumbnailPath = "/x.jpg",
        };

        var preset = PresetMapper.ToPreset(snap, "id-1", "my preset", created, updated);

        Assert.Equal("id-1", preset.Id);
        Assert.Equal("my preset", preset.Name);
        Assert.Equal(created, preset.CreatedAt);
        Assert.Equal(updated, preset.UpdatedAt);
        Assert.Equal("t", preset.Title);
        Assert.Equal("/x.jpg", preset.ThumbnailPath);
        Assert.Equal(new[] { "one" }, preset.Tags.ToArray());
        Assert.Equal(42, preset.BroadcastStreamDelayMs);
    }

    [Fact]
    public void RoundTrip_PresetToSnapshotToPreset_PreservesValues()
    {
        var original = new Preset
        {
            Id = "id",
            Name = "name",
            CreatedAt = DateTimeOffset.Parse("2026-04-23T00:00:00Z"),
            UpdatedAt = DateTimeOffset.Parse("2026-04-23T01:00:00Z"),
            Title = "roundtrip",
            Tags = new[] { "x", "y", "z" },
            BroadcastStreamDelayMs = 99,
            ThumbnailPath = "/p.jpg",
        };

        var snap = PresetMapper.ToSnapshot(original);
        var rebuilt = PresetMapper.ToPreset(snap, original.Id, original.Name, original.CreatedAt, original.UpdatedAt);

        Assert.Equal(original.Id, rebuilt.Id);
        Assert.Equal(original.Name, rebuilt.Name);
        Assert.Equal(original.CreatedAt, rebuilt.CreatedAt);
        Assert.Equal(original.UpdatedAt, rebuilt.UpdatedAt);
        Assert.Equal(original.Title, rebuilt.Title);
        Assert.Equal(original.BroadcastStreamDelayMs, rebuilt.BroadcastStreamDelayMs);
        Assert.Equal(original.ThumbnailPath, rebuilt.ThumbnailPath);
        Assert.Equal(original.Tags.ToArray(), rebuilt.Tags.ToArray());
    }
}
