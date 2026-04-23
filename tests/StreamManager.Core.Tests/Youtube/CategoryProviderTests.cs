using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using StreamManager.Core;
using StreamManager.Core.Youtube;
using Xunit;

namespace StreamManager.Core.Tests.Youtube;

public class CategoryProviderTests : IDisposable
{
    private readonly string _appDataRoot;
    private readonly AppPaths _paths;
    private readonly FakeTimeProvider _clock;

    public CategoryProviderTests()
    {
        _appDataRoot = Path.Combine(
            Path.GetTempPath(),
            $"sm-categories-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_appDataRoot);
        _paths = new AppPaths(_appDataRoot);
        _paths.EnsureDirectoriesExist();
        _clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-04-23T18:00:00Z"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_appDataRoot))
        {
            try { Directory.Delete(_appDataRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task EnsureLoaded_NoCache_CallsApiAndWritesCache()
    {
        var client = new FakeYouTubeReferenceClient();
        client.CategoriesByRegion["US"] = NewCategories(("20", "Gaming"), ("28", "Science & Tech"));
        var provider = NewProvider(client);

        await provider.EnsureLoadedAsync(CancellationToken.None);

        Assert.Equal(1, client.CategoryCallCount);
        Assert.Equal(2, provider.Current.Count);
        Assert.True(File.Exists(Path.Combine(_paths.CacheDirectory, "categories.json")));
    }

    [Fact]
    public async Task EnsureLoaded_FreshCache_ServesFromDiskWithoutApiCall()
    {
        // Seed: write a cache file retrieved "just now" so the TTL check passes.
        WriteCacheFile(new Dictionary<string, CategoriesCacheEntry>
        {
            ["US"] = new()
            {
                RetrievedAt = _clock.GetUtcNow(),
                Items = NewCategories(("20", "Gaming")).ToList(),
            },
        });

        var client = new FakeYouTubeReferenceClient();
        var provider = NewProvider(client);

        await provider.EnsureLoadedAsync(CancellationToken.None);

        Assert.Equal(0, client.CategoryCallCount);
        Assert.Single(provider.Current);
    }

    [Fact]
    public async Task EnsureLoaded_StaleCache_ReturnsCachedThenBackgroundRefreshes()
    {
        // Seed a stale entry (31 days old).
        var stale = _clock.GetUtcNow().AddDays(-31);
        WriteCacheFile(new Dictionary<string, CategoriesCacheEntry>
        {
            ["US"] = new()
            {
                RetrievedAt = stale,
                Items = NewCategories(("99", "old-cached")).ToList(),
            },
        });

        var gate = new TaskCompletionSource();
        var client = new FakeYouTubeReferenceClient { CategoryGate = gate };
        client.CategoriesByRegion["US"] = NewCategories(("20", "Gaming"));
        var provider = NewProvider(client);

        await provider.EnsureLoadedAsync(CancellationToken.None);

        // Cached data returns synchronously while the background refresh
        // is still blocked on the gate.
        Assert.Single(provider.Current);
        Assert.Equal("99", provider.Current[0].Id);

        // Release the background refresh and let it replace Current.
        gate.SetResult();
        await provider.BackgroundRefreshTask;
        Assert.Equal("20", provider.Current[0].Id);
        Assert.Equal(1, client.CategoryCallCount);
    }

    [Fact]
    public async Task Refresh_OverwritesCacheEvenWhenFresh()
    {
        WriteCacheFile(new Dictionary<string, CategoriesCacheEntry>
        {
            ["US"] = new()
            {
                RetrievedAt = _clock.GetUtcNow(),
                Items = NewCategories(("1", "old")).ToList(),
            },
        });

        var client = new FakeYouTubeReferenceClient();
        client.CategoriesByRegion["US"] = NewCategories(("20", "Gaming"));
        var provider = NewProvider(client);

        await provider.RefreshAsync(CancellationToken.None);

        Assert.Equal(1, client.CategoryCallCount);
        Assert.Equal("20", provider.Current[0].Id);
    }

    [Fact]
    public async Task SetRegionCode_SeparateCacheKey_PreviousRegionUntouched()
    {
        WriteCacheFile(new Dictionary<string, CategoriesCacheEntry>
        {
            ["US"] = new()
            {
                RetrievedAt = _clock.GetUtcNow(),
                Items = NewCategories(("20", "Gaming US")).ToList(),
            },
        });

        var client = new FakeYouTubeReferenceClient();
        client.CategoriesByRegion["DE"] = NewCategories(("20", "Gaming DE"));
        var provider = NewProvider(client);
        await provider.EnsureLoadedAsync(CancellationToken.None);

        await provider.SetRegionCodeAsync("DE", CancellationToken.None);

        Assert.Equal("Gaming DE", provider.Current[0].Snippet!.Title);

        // US entry is still on disk.
        var path = Path.Combine(_paths.CacheDirectory, "categories.json");
        var doc = JsonSerializer.Deserialize<CategoriesCacheFile>(
            File.ReadAllText(path), ReferenceCacheJson.Options)!;
        Assert.True(doc.Entries.ContainsKey("US"));
        Assert.True(doc.Entries.ContainsKey("DE"));
        Assert.Equal("Gaming US", doc.Entries["US"].Items[0].Snippet!.Title);
    }

    [Fact]
    public async Task CorruptCache_TreatedAsMissing_TriggersApiFetch()
    {
        File.WriteAllText(
            Path.Combine(_paths.CacheDirectory, "categories.json"),
            "{ this is not valid json");

        var client = new FakeYouTubeReferenceClient();
        client.CategoriesByRegion["US"] = NewCategories(("20", "Gaming"));
        var provider = NewProvider(client);

        await provider.EnsureLoadedAsync(CancellationToken.None);

        Assert.Equal(1, client.CategoryCallCount);
        Assert.Single(provider.Current);
    }

    [Fact]
    public async Task ApiFailureWithNoCache_SurfacesErrorAndLeavesCurrentEmpty()
    {
        var client = new FakeYouTubeReferenceClient
        {
            ThrowOnCategories = new InvalidOperationException("network down"),
        };
        var provider = NewProvider(client);

        await provider.EnsureLoadedAsync(CancellationToken.None);

        Assert.Empty(provider.Current);
        Assert.NotNull(provider.LastErrorMessage);
    }

    [Fact]
    public void CacheFormat_ReadsKnownGoodFixture()
    {
        var json = """
            {
              "schemaVersion": 1,
              "entries": {
                "US": {
                  "retrievedAt": "2026-04-23T17:30:00+00:00",
                  "items": [
                    { "id": "20", "kind": "youtube#videoCategory",
                      "snippet": { "title": "Gaming", "assignable": true,
                                   "channelId": "UCBR8-60-B28hp2BmDPdntcQ" } },
                    { "id": "28", "kind": "youtube#videoCategory",
                      "snippet": { "title": "Science & Technology",
                                   "assignable": true } }
                  ]
                }
              }
            }
            """;

        var file = JsonSerializer.Deserialize<CategoriesCacheFile>(
            json, ReferenceCacheJson.Options)!;

        Assert.Single(file.Entries);
        var entry = file.Entries["US"];
        Assert.Equal(2, entry.Items.Count);
        Assert.Equal("20", entry.Items[0].Id);
        Assert.Equal("Gaming", entry.Items[0].Snippet!.Title);
        Assert.True(entry.Items[0].Snippet!.Assignable);
    }

    [Fact]
    public void CacheFormat_RoundTripPreservesUnknownFields()
    {
        // Future YouTube additions land in JsonExtensionData and survive
        // a read-then-write cycle — so "remap without a re-fetch" holds.
        var json = """
            {
              "schemaVersion": 1,
              "entries": {
                "US": {
                  "retrievedAt": "2026-04-23T17:30:00+00:00",
                  "items": [
                    { "id": "20", "snippet": { "title": "Gaming",
                                               "assignable": true,
                                               "newFutureField": "yes" } }
                  ],
                  "futureTopLevelField": "kept"
                }
              },
              "topLevelFuture": 42
            }
            """;

        var file = JsonSerializer.Deserialize<CategoriesCacheFile>(
            json, ReferenceCacheJson.Options)!;
        var reserialized = JsonSerializer.Serialize(file, ReferenceCacheJson.Options);

        Assert.Contains("newFutureField", reserialized);
        Assert.Contains("futureTopLevelField", reserialized);
        Assert.Contains("topLevelFuture", reserialized);
    }

    private CategoryProvider NewProvider(IYouTubeClient client) =>
        new CategoryProvider(
            _paths, client, _clock, NullLogger<CategoryProvider>.Instance);

    private static IReadOnlyList<VideoCategoryListItem> NewCategories(
        params (string Id, string Title)[] entries)
    {
        return entries
            .Select(e => new VideoCategoryListItem
            {
                Id = e.Id,
                Kind = "youtube#videoCategory",
                Snippet = new VideoCategorySnippetDto
                {
                    Title = e.Title,
                    Assignable = true,
                },
            })
            .ToArray();
    }

    private void WriteCacheFile(Dictionary<string, CategoriesCacheEntry> entries)
    {
        var file = new CategoriesCacheFile { Entries = entries };
        var json = JsonSerializer.Serialize(file, ReferenceCacheJson.Options);
        File.WriteAllText(
            Path.Combine(_paths.CacheDirectory, "categories.json"),
            json);
    }
}
