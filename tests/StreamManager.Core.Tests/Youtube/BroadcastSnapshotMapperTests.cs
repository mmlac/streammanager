using System;
using System.Collections.Generic;
using Google.Apis.YouTube.v3.Data;
using StreamManager.Core.Youtube;
using Xunit;

namespace StreamManager.Core.Tests.Youtube;

public class BroadcastSnapshotMapperTests
{
    // ---- §5: liveBroadcast snippet fields ----

    [Fact]
    public void MapsTitleDescriptionAndScheduledTimes()
    {
        var start = DateTimeOffset.Parse("2026-04-23T18:00:00Z");
        var end = DateTimeOffset.Parse("2026-04-23T21:00:00Z");
        var broadcast = NewBroadcast(s =>
        {
            s.Title = "Title A";
            s.Description = "Desc A";
            s.ScheduledStartTimeDateTimeOffset = start;
            s.ScheduledEndTimeDateTimeOffset = end;
        });

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        Assert.Equal("Title A", snap.Title);
        Assert.Equal("Desc A", snap.Description);
        Assert.Equal(start, snap.ScheduledStartTime);
        Assert.Equal(end, snap.ScheduledEndTime);
    }

    [Fact]
    public void MapsBroadcastAndVideoIds()
    {
        var broadcast = new LiveBroadcast { Id = "bc-1", Snippet = new LiveBroadcastSnippet() };
        var video = new Video { Id = "vid-1" };

        var snap = BroadcastSnapshotMapper.Map(broadcast, video);

        Assert.Equal("bc-1", snap.BroadcastId);
        Assert.Equal("vid-1", snap.VideoId);
    }

    // ---- §5: status fields ----

    [Theory]
    [InlineData("public")]
    [InlineData("unlisted")]
    [InlineData("private")]
    public void MapsPrivacyStatus(string value)
    {
        var broadcast = NewBroadcast(status: s => s.PrivacyStatus = value);

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        Assert.Equal(value, snap.PrivacyStatus);
    }

    [Fact]
    public void PrivacyStatus_FallsBackToPublicWhenMissing()
    {
        var broadcast = NewBroadcast(status: s => s.PrivacyStatus = null);

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        Assert.Equal("public", snap.PrivacyStatus);
    }

    [Fact]
    public void MapsSelfDeclaredMadeForKids_PrefersSelfDeclaredOverMadeForKids()
    {
        var broadcast = NewBroadcast(status: s =>
        {
            s.SelfDeclaredMadeForKids = true;
            s.MadeForKids = false;
        });

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        Assert.True(snap.SelfDeclaredMadeForKids);
    }

    [Fact]
    public void MapsSelfDeclaredMadeForKids_FallsBackToMadeForKidsWhenSelfDeclaredNull()
    {
        var broadcast = NewBroadcast(status: s =>
        {
            s.SelfDeclaredMadeForKids = null;
            s.MadeForKids = true;
        });

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        Assert.True(snap.SelfDeclaredMadeForKids);
    }

    // ---- §5: contentDetails fields ----

    [Fact]
    public void MapsContentDetailsFlags()
    {
        var broadcast = NewBroadcast(cd: cd =>
        {
            cd.EnableAutoStart = false;
            cd.EnableAutoStop = false;
            cd.EnableClosedCaptions = true;
            cd.EnableDvr = false;
            cd.EnableEmbed = false;
            cd.RecordFromStart = false;
            cd.StartWithSlate = true;
            cd.EnableContentEncryption = true;
            cd.EnableLowLatency = true;
        });

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        Assert.False(snap.EnableAutoStart);
        Assert.False(snap.EnableAutoStop);
        Assert.True(snap.EnableClosedCaptions);
        Assert.False(snap.EnableDvr);
        Assert.False(snap.EnableEmbed);
        Assert.False(snap.RecordFromStart);
        Assert.True(snap.StartWithSlate);
        Assert.True(snap.EnableContentEncryption);
        Assert.True(snap.EnableLowLatency);
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("low")]
    [InlineData("ultraLow")]
    public void MapsLatencyPreference(string value)
    {
        var broadcast = NewBroadcast(cd: cd => cd.LatencyPreference = value);

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        Assert.Equal(value, snap.LatencyPreference);
    }

    [Fact]
    public void LatencyPreference_FallsBackToNormalWhenMissing()
    {
        var broadcast = NewBroadcast(cd: cd => cd.LatencyPreference = null);

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        Assert.Equal("normal", snap.LatencyPreference);
    }

    [Theory]
    [InlineData("rectangular")]
    [InlineData("360")]
    [InlineData("mesh")]
    public void MapsProjection(string value)
    {
        var broadcast = NewBroadcast(cd: cd => cd.Projection = value);

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        Assert.Equal(value, snap.Projection);
    }

    [Theory]
    [InlineData("mono")]
    [InlineData("left_right")]
    [InlineData("top_bottom")]
    public void MapsStereoLayout(string value)
    {
        var broadcast = NewBroadcast(cd: cd => cd.StereoLayout = value);

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        Assert.Equal(value, snap.StereoLayout);
    }

    [Theory]
    [InlineData("closedCaptionsDisabled")]
    [InlineData("closedCaptionsHttpPost")]
    [InlineData("closedCaptionsEmbedInVideo")]
    public void MapsClosedCaptionsType(string value)
    {
        var broadcast = NewBroadcast(cd: cd => cd.ClosedCaptionsType = value);

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        Assert.Equal(value, snap.ClosedCaptionsType);
    }

    [Fact]
    public void MapsMonitorStreamFields()
    {
        var broadcast = NewBroadcast(cd: cd => cd.MonitorStream = new MonitorStreamInfo
        {
            EnableMonitorStream = false,
            BroadcastStreamDelayMs = 2500,
        });

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        Assert.False(snap.EnableMonitorStream);
        Assert.Equal(2500, snap.BroadcastStreamDelayMs);
    }

    [Fact]
    public void MonitorStream_MissingFallsBackToDefaults()
    {
        var broadcast = NewBroadcast(cd: cd => cd.MonitorStream = null);

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        Assert.True(snap.EnableMonitorStream);
        Assert.Equal(0, snap.BroadcastStreamDelayMs);
    }

    [Fact]
    public void BroadcastStreamDelay_NegativeValuesClampToZero()
    {
        var broadcast = NewBroadcast(cd: cd => cd.MonitorStream = new MonitorStreamInfo
        {
            BroadcastStreamDelayMs = -5,
        });

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        Assert.Equal(0, snap.BroadcastStreamDelayMs);
    }

    // ---- §5: contentDetails edge cases ----

    [Fact]
    public void MissingContentDetails_FallsBackToDefaults()
    {
        var broadcast = new LiveBroadcast
        {
            Id = "bc",
            Snippet = new LiveBroadcastSnippet { Title = "t" },
            Status = new LiveBroadcastStatus { PrivacyStatus = "public" },
            ContentDetails = null,
        };

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        // defaults from §5
        Assert.True(snap.EnableAutoStart);
        Assert.True(snap.EnableAutoStop);
        Assert.False(snap.EnableClosedCaptions);
        Assert.True(snap.EnableDvr);
        Assert.True(snap.EnableEmbed);
        Assert.True(snap.RecordFromStart);
        Assert.False(snap.StartWithSlate);
        Assert.Equal("normal", snap.LatencyPreference);
        Assert.Equal("rectangular", snap.Projection);
        Assert.Equal("mono", snap.StereoLayout);
        Assert.Equal("closedCaptionsDisabled", snap.ClosedCaptionsType);
        Assert.True(snap.EnableMonitorStream);
    }

    // ---- §5: video snippet fields ----

    [Fact]
    public void MapsCategoryId()
    {
        var video = NewVideo(s => s.CategoryId = "20");

        var snap = BroadcastSnapshotMapper.Map(NewBroadcast(), video);

        Assert.Equal("20", snap.CategoryId);
    }

    [Fact]
    public void MapsTags()
    {
        var video = NewVideo(s => s.Tags = new List<string> { "elden ring", "soulslike" });

        var snap = BroadcastSnapshotMapper.Map(NewBroadcast(), video);

        Assert.Equal(new[] { "elden ring", "soulslike" }, snap.Tags);
    }

    [Fact]
    public void NullTags_MapToEmptyList()
    {
        var video = NewVideo(s => s.Tags = null);

        var snap = BroadcastSnapshotMapper.Map(NewBroadcast(), video);

        Assert.Empty(snap.Tags);
    }

    [Fact]
    public void MapsDefaultLanguages()
    {
        var video = NewVideo(s =>
        {
            s.DefaultLanguage = "en";
            s.DefaultAudioLanguage = "en-US";
        });

        var snap = BroadcastSnapshotMapper.Map(NewBroadcast(), video);

        Assert.Equal("en", snap.DefaultLanguage);
        Assert.Equal("en-US", snap.DefaultAudioLanguage);
    }

    [Fact]
    public void EmptyStringLanguages_MapToNull()
    {
        var video = NewVideo(s =>
        {
            s.DefaultLanguage = "";
            s.DefaultAudioLanguage = "";
        });

        var snap = BroadcastSnapshotMapper.Map(NewBroadcast(), video);

        Assert.Null(snap.DefaultLanguage);
        Assert.Null(snap.DefaultAudioLanguage);
    }

    // ---- Localizations: absence must not break the mapping ----

    [Fact]
    public void MissingLocalizations_DoesNotThrow()
    {
        var video = new Video { Id = "vid", Snippet = new VideoSnippet { CategoryId = "20" }, Localizations = null };

        var ex = Record.Exception(() => BroadcastSnapshotMapper.Map(NewBroadcast(), video));

        Assert.Null(ex);
    }

    // ---- Thumbnail URL selection ----

    [Fact]
    public void PrefersMaxresThumbnailFromBroadcast()
    {
        var broadcast = NewBroadcast(s => s.Thumbnails = new ThumbnailDetails
        {
            Maxres = new Thumbnail { Url = "https://img/maxres.jpg" },
            High = new Thumbnail { Url = "https://img/high.jpg" },
            Default__ = new Thumbnail { Url = "https://img/default.jpg" },
        });

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        Assert.Equal("https://img/maxres.jpg", snap.ThumbnailUrl);
    }

    [Fact]
    public void FallsBackThroughThumbnailSizes()
    {
        var broadcast = NewBroadcast(s => s.Thumbnails = new ThumbnailDetails
        {
            High = new Thumbnail { Url = "https://img/high.jpg" },
        });

        var snap = BroadcastSnapshotMapper.Map(broadcast, NewVideo());

        Assert.Equal("https://img/high.jpg", snap.ThumbnailUrl);
    }

    [Fact]
    public void FallsBackToVideoThumbnailsWhenBroadcastHasNone()
    {
        var broadcast = NewBroadcast(s => s.Thumbnails = null);
        var video = NewVideo(s => s.Thumbnails = new ThumbnailDetails
        {
            Default__ = new Thumbnail { Url = "https://img/video-default.jpg" },
        });

        var snap = BroadcastSnapshotMapper.Map(broadcast, video);

        Assert.Equal("https://img/video-default.jpg", snap.ThumbnailUrl);
    }

    [Fact]
    public void NoThumbnailsAnywhere_ReturnsNull()
    {
        var broadcast = NewBroadcast(s => s.Thumbnails = null);
        var video = NewVideo(s => s.Thumbnails = null);

        var snap = BroadcastSnapshotMapper.Map(broadcast, video);

        Assert.Null(snap.ThumbnailUrl);
    }

    // ---- Null arg guards ----

    [Fact]
    public void ThrowsOnNullBroadcast()
    {
        Assert.Throws<ArgumentNullException>(
            () => BroadcastSnapshotMapper.Map(null!, NewVideo()));
    }

    [Fact]
    public void ThrowsOnNullVideo()
    {
        Assert.Throws<ArgumentNullException>(
            () => BroadcastSnapshotMapper.Map(NewBroadcast(), null!));
    }

    // ---- Helpers ----

    private static LiveBroadcast NewBroadcast(
        Action<LiveBroadcastSnippet>? snippet = null,
        Action<LiveBroadcastStatus>? status = null,
        Action<LiveBroadcastContentDetails>? cd = null)
    {
        var s = new LiveBroadcastSnippet { Title = "", Description = "" };
        snippet?.Invoke(s);
        var st = new LiveBroadcastStatus { PrivacyStatus = "public" };
        status?.Invoke(st);
        var cdObj = new LiveBroadcastContentDetails();
        cd?.Invoke(cdObj);
        return new LiveBroadcast
        {
            Id = "bc-id",
            Snippet = s,
            Status = st,
            ContentDetails = cdObj,
        };
    }

    private static Video NewVideo(Action<VideoSnippet>? snippet = null)
    {
        var s = new VideoSnippet();
        snippet?.Invoke(s);
        return new Video
        {
            Id = "vid-id",
            Snippet = s,
        };
    }
}
