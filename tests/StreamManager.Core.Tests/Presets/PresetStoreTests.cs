using System.Text.Json;
using StreamManager.Core;
using StreamManager.Core.Presets;
using Xunit;

namespace StreamManager.Core.Tests.Presets;

public class PresetStoreTests : IDisposable
{
    private readonly string _appDataRoot;
    private readonly AppPaths _paths;

    public PresetStoreTests()
    {
        _appDataRoot = Path.Combine(
            Path.GetTempPath(),
            $"sm-preset-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_appDataRoot);
        _paths = new AppPaths(_appDataRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_appDataRoot))
        {
            try { Directory.Delete(_appDataRoot, recursive: true); }
            catch { }
        }
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsEmptyAtCurrentVersion()
    {
        var store = new PresetStore(_paths);

        var file = store.Load();

        Assert.Equal(PresetStore.CurrentSchemaVersion, file.SchemaVersion);
        Assert.Empty(file.Presets);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsEverySection5Field()
    {
        var store = new PresetStore(_paths);

        var now = new DateTimeOffset(2026, 04, 23, 18, 0, 0, TimeSpan.Zero);
        var preset = new Preset
        {
            Id = "p-1",
            Name = "Elden Ring — chill",
            CreatedAt = now,
            UpdatedAt = now.AddMinutes(5),

            Title = "Elden Ring — blind",
            Description = "Blind playthrough, day 3",
            ScheduledStartTime = now.AddHours(1),
            ScheduledEndTime = now.AddHours(4),

            PrivacyStatus = "unlisted",
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
            LatencyPreference = "ultraLow",
            EnableMonitorStream = false,
            BroadcastStreamDelayMs = 4500,
            Projection = "360",
            StereoLayout = "left_right",
            ClosedCaptionsType = "closedCaptionsHttpPost",

            CategoryId = "20",
            Tags = new[] { "elden ring", "soulslike" },
            DefaultLanguage = "en",
            DefaultAudioLanguage = "en",

            ThumbnailPath = null,
        };

        store.Save(new PresetsFile(PresetStore.CurrentSchemaVersion, new[] { preset }));

        var reloaded = store.Load();
        Assert.Single(reloaded.Presets);
        var got = reloaded.Presets[0];

        Assert.Equal(preset.Id, got.Id);
        Assert.Equal(preset.Name, got.Name);
        Assert.Equal(preset.CreatedAt, got.CreatedAt);
        Assert.Equal(preset.UpdatedAt, got.UpdatedAt);
        Assert.Equal(preset.Title, got.Title);
        Assert.Equal(preset.Description, got.Description);
        Assert.Equal(preset.ScheduledStartTime, got.ScheduledStartTime);
        Assert.Equal(preset.ScheduledEndTime, got.ScheduledEndTime);
        Assert.Equal(preset.PrivacyStatus, got.PrivacyStatus);
        Assert.Equal(preset.SelfDeclaredMadeForKids, got.SelfDeclaredMadeForKids);
        Assert.Equal(preset.EnableAutoStart, got.EnableAutoStart);
        Assert.Equal(preset.EnableAutoStop, got.EnableAutoStop);
        Assert.Equal(preset.EnableClosedCaptions, got.EnableClosedCaptions);
        Assert.Equal(preset.EnableDvr, got.EnableDvr);
        Assert.Equal(preset.EnableEmbed, got.EnableEmbed);
        Assert.Equal(preset.RecordFromStart, got.RecordFromStart);
        Assert.Equal(preset.StartWithSlate, got.StartWithSlate);
        Assert.Equal(preset.EnableContentEncryption, got.EnableContentEncryption);
        Assert.Equal(preset.EnableLowLatency, got.EnableLowLatency);
        Assert.Equal(preset.LatencyPreference, got.LatencyPreference);
        Assert.Equal(preset.EnableMonitorStream, got.EnableMonitorStream);
        Assert.Equal(preset.BroadcastStreamDelayMs, got.BroadcastStreamDelayMs);
        Assert.Equal(preset.Projection, got.Projection);
        Assert.Equal(preset.StereoLayout, got.StereoLayout);
        Assert.Equal(preset.ClosedCaptionsType, got.ClosedCaptionsType);
        Assert.Equal(preset.CategoryId, got.CategoryId);
        Assert.Equal(preset.Tags.ToArray(), got.Tags.ToArray());
        Assert.Equal(preset.DefaultLanguage, got.DefaultLanguage);
        Assert.Equal(preset.DefaultAudioLanguage, got.DefaultAudioLanguage);
        Assert.Null(got.ThumbnailPath);
    }

    [Fact]
    public void SaveThenLoad_PreservesThumbnailPathAndEmptyTags()
    {
        var store = new PresetStore(_paths);

        var preset = new Preset
        {
            Id = "p-2",
            Name = "no tags",
            Tags = Array.Empty<string>(),
            ThumbnailPath = "/Users/me/Pictures/x.jpg",
        };

        store.Save(new PresetsFile(PresetStore.CurrentSchemaVersion, new[] { preset }));

        var got = store.Load().Presets[0];
        Assert.Equal("/Users/me/Pictures/x.jpg", got.ThumbnailPath);
        Assert.Empty(got.Tags);
    }

    [Fact]
    public void Load_SchemaVersionZero_ThrowsUnknownOlder()
    {
        File.WriteAllText(_paths.PresetsFilePath, "{\"schemaVersion\":0,\"presets\":[]}");
        var store = new PresetStore(_paths);

        var ex = Assert.Throws<PresetStoreException>(() => store.Load());
        Assert.Equal(PresetStoreErrorCode.UnknownOlder, ex.Code);
    }

    [Fact]
    public void Load_SchemaVersionInTheFuture_ThrowsNewerThanSupported()
    {
        File.WriteAllText(_paths.PresetsFilePath, "{\"schemaVersion\":2,\"presets\":[]}");
        var store = new PresetStore(_paths);

        var ex = Assert.Throws<PresetStoreException>(() => store.Load());
        Assert.Equal(PresetStoreErrorCode.NewerThanSupported, ex.Code);
        Assert.Contains("newer", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_CorruptJson_ThrowsMalformed()
    {
        File.WriteAllText(_paths.PresetsFilePath, "{ not valid json");
        var store = new PresetStore(_paths);

        var ex = Assert.Throws<PresetStoreException>(() => store.Load());
        Assert.Equal(PresetStoreErrorCode.Malformed, ex.Code);
    }

    [Fact]
    public void Load_EmptyFile_ThrowsMalformed()
    {
        File.WriteAllText(_paths.PresetsFilePath, "");
        var store = new PresetStore(_paths);

        var ex = Assert.Throws<PresetStoreException>(() => store.Load());
        Assert.Equal(PresetStoreErrorCode.Malformed, ex.Code);
    }

    [Fact]
    public void AfterMalformedFile_SaveAsRecoversTheStore()
    {
        // Corrupt JSON is rejected on load, but a subsequent Save must be
        // able to stomp the broken file so the user can recover via the
        // Save-as flow (notes "app continues; user can still Save-as").
        File.WriteAllText(_paths.PresetsFilePath, "{ not valid json");
        var store = new PresetStore(_paths);

        var preset = new Preset { Id = "p-1", Name = "fresh" };
        store.Save(new PresetsFile(PresetStore.CurrentSchemaVersion, new[] { preset }));

        var loaded = store.Load();
        Assert.Single(loaded.Presets);
        Assert.Equal("fresh", loaded.Presets[0].Name);
    }

    [Fact]
    public void AtomicWrite_CrashBeforeRename_LeavesOldFileIntact_AndCleansUpTemp()
    {
        var store = new PresetStore(_paths);

        var original = new Preset { Id = "p-1", Name = "original" };
        store.Save(new PresetsFile(PresetStore.CurrentSchemaVersion, new[] { original }));

        // Inject a simulated crash: throw immediately before the atomic rename.
        var crashingStore = new PresetStore(_paths, new ThrowingFaultInjector());
        var attempted = new Preset { Id = "p-1", Name = "attempted replacement" };
        Assert.ThrowsAny<Exception>(() =>
            crashingStore.Save(new PresetsFile(PresetStore.CurrentSchemaVersion, new[] { attempted })));

        // Old file still on disk with its previous content.
        Assert.True(File.Exists(_paths.PresetsFilePath));
        var tempPath = _paths.PresetsFilePath + ".tmp";
        Assert.True(File.Exists(tempPath), "Temp file should linger after simulated crash.");

        // Fresh store's Load sweeps the stale temp file.
        var recovered = new PresetStore(_paths).Load();
        Assert.Single(recovered.Presets);
        Assert.Equal("original", recovered.Presets[0].Name);
        Assert.False(File.Exists(tempPath), "Stale temp file should be cleaned up by the next Load.");
    }

    private sealed class ThrowingFaultInjector : IAtomicWriteFaultInjector
    {
        public void BeforeRename(string tempPath, string targetPath) =>
            throw new InvalidOperationException("simulated crash before rename");
    }

    [Fact]
    public void Save_EnvelopeAlwaysWritesCurrentSchemaVersion()
    {
        var store = new PresetStore(_paths);
        store.Save(new PresetsFile(PresetStore.CurrentSchemaVersion, Array.Empty<Preset>()));

        var raw = File.ReadAllText(_paths.PresetsFilePath);
        using var doc = JsonDocument.Parse(raw);
        Assert.Equal(
            PresetStore.CurrentSchemaVersion,
            doc.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void Save_RefusesToPersistMismatchedSchemaVersion()
    {
        var store = new PresetStore(_paths);
        Assert.Throws<ArgumentException>(() =>
            store.Save(new PresetsFile(SchemaVersion: 2, Presets: Array.Empty<Preset>())));
    }
}
