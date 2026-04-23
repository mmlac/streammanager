using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using StreamManager.Core;
using StreamManager.Core.Youtube;
using Xunit;

namespace StreamManager.Core.Tests.Youtube;

public class LanguageProviderTests : IDisposable
{
    private readonly string _appDataRoot;
    private readonly AppPaths _paths;
    private readonly FakeTimeProvider _clock;

    public LanguageProviderTests()
    {
        _appDataRoot = Path.Combine(
            Path.GetTempPath(),
            $"sm-languages-{Guid.NewGuid():N}");
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
        var client = new FakeYouTubeReferenceClient
        {
            Languages = NewLanguages(("en", "English"), ("de", "German")),
        };
        var provider = NewProvider(client);

        await provider.EnsureLoadedAsync(CancellationToken.None);

        Assert.Equal(1, client.LanguageCallCount);
        Assert.Equal(2, provider.Current.Count);
        Assert.True(File.Exists(Path.Combine(_paths.CacheDirectory, "languages.json")));
    }

    [Fact]
    public async Task EnsureLoaded_FreshCache_ServesFromDiskWithoutApiCall()
    {
        WriteCacheFile(_clock.GetUtcNow(), NewLanguages(("en", "English")));

        var client = new FakeYouTubeReferenceClient();
        var provider = NewProvider(client);

        await provider.EnsureLoadedAsync(CancellationToken.None);

        Assert.Equal(0, client.LanguageCallCount);
        Assert.Single(provider.Current);
    }

    [Fact]
    public async Task EnsureLoaded_StaleCache_ReturnsCachedThenBackgroundRefreshes()
    {
        WriteCacheFile(_clock.GetUtcNow().AddDays(-31), NewLanguages(("old", "Old")));

        var gate = new TaskCompletionSource();
        var client = new FakeYouTubeReferenceClient
        {
            Languages = NewLanguages(("en", "English")),
            LanguageGate = gate,
        };
        var provider = NewProvider(client);

        await provider.EnsureLoadedAsync(CancellationToken.None);

        Assert.Equal("old", provider.Current[0].Id);

        gate.SetResult();
        await provider.BackgroundRefreshTask;
        Assert.Equal("en", provider.Current[0].Id);
        Assert.Equal(1, client.LanguageCallCount);
    }

    [Fact]
    public async Task Refresh_OverwritesCacheEvenWhenFresh()
    {
        WriteCacheFile(_clock.GetUtcNow(), NewLanguages(("old", "Old")));

        var client = new FakeYouTubeReferenceClient
        {
            Languages = NewLanguages(("en", "English")),
        };
        var provider = NewProvider(client);

        await provider.RefreshAsync(CancellationToken.None);

        Assert.Equal("en", provider.Current[0].Id);
    }

    [Fact]
    public async Task ApiFailureWithNoCache_SurfacesError()
    {
        var client = new FakeYouTubeReferenceClient
        {
            ThrowOnLanguages = new InvalidOperationException("network down"),
        };
        var provider = NewProvider(client);

        await provider.EnsureLoadedAsync(CancellationToken.None);

        Assert.Empty(provider.Current);
        Assert.NotNull(provider.LastErrorMessage);
    }

    [Fact]
    public async Task CorruptCache_TreatedAsMissing_TriggersApiFetch()
    {
        File.WriteAllText(
            Path.Combine(_paths.CacheDirectory, "languages.json"),
            "{ this is not valid json");

        var client = new FakeYouTubeReferenceClient
        {
            Languages = NewLanguages(("en", "English")),
        };
        var provider = NewProvider(client);

        await provider.EnsureLoadedAsync(CancellationToken.None);

        Assert.Equal(1, client.LanguageCallCount);
        Assert.Single(provider.Current);
    }

    [Fact]
    public void CacheFormat_RoundTripPreservesUnknownFields()
    {
        var json = """
            {
              "schemaVersion": 1,
              "retrievedAt": "2026-04-23T17:30:00+00:00",
              "items": [
                { "id": "en", "snippet": { "hl": "en", "name": "English",
                                           "futureField": "preserved" } }
              ],
              "topLevelFuture": "kept"
            }
            """;

        var file = JsonSerializer.Deserialize<LanguagesCacheFile>(
            json, ReferenceCacheJson.Options)!;
        var reserialized = JsonSerializer.Serialize(file, ReferenceCacheJson.Options);

        Assert.Contains("futureField", reserialized);
        Assert.Contains("topLevelFuture", reserialized);
    }

    private LanguageProvider NewProvider(IYouTubeClient client) =>
        new LanguageProvider(
            _paths, client, _clock, NullLogger<LanguageProvider>.Instance);

    private static IReadOnlyList<I18nLanguageListItem> NewLanguages(
        params (string Hl, string Name)[] entries)
    {
        return entries
            .Select(e => new I18nLanguageListItem
            {
                Id = e.Hl,
                Kind = "youtube#i18nLanguage",
                Snippet = new I18nLanguageSnippetDto
                {
                    Hl = e.Hl,
                    Name = e.Name,
                },
            })
            .ToArray();
    }

    private void WriteCacheFile(
        DateTimeOffset retrievedAt,
        IReadOnlyList<I18nLanguageListItem> items)
    {
        var file = new LanguagesCacheFile
        {
            RetrievedAt = retrievedAt,
            Items = items.ToList(),
        };
        var json = JsonSerializer.Serialize(file, ReferenceCacheJson.Options);
        File.WriteAllText(
            Path.Combine(_paths.CacheDirectory, "languages.json"),
            json);
    }
}
