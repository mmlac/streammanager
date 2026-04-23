using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamManager.Core.Youtube;

// On-disk shape for `<AppData>/streammanager/cache/categories.json`.
// One file holds every region's most-recent response, keyed by region
// code; switching regions never clears the previous region's cache.
internal sealed class CategoriesCacheFile
{
    public int SchemaVersion { get; set; } = 1;

    public Dictionary<string, CategoriesCacheEntry> Entries { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

internal sealed class CategoriesCacheEntry
{
    public DateTimeOffset RetrievedAt { get; set; }
    public List<VideoCategoryListItem> Items { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

// On-disk shape for `<AppData>/streammanager/cache/languages.json`.
internal sealed class LanguagesCacheFile
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset RetrievedAt { get; set; }
    public List<I18nLanguageListItem> Items { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

// Shared JSON options for cache I/O. Mirrors the camelCase convention
// YouTube itself uses in API responses so field names in the cache file
// match the JSON the user would see calling the API directly — keeps
// "raw-response" semantics readable.
internal static class ReferenceCacheJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
