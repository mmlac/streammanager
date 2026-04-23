using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamManager.Core.Youtube;

// Cache row for a single item in the `videoCategories.list` response.
// Mirrors the fields documented by the API; JsonExtensionData catches
// anything YouTube adds later so the file can round-trip without loss
// (design slice 6: "cache files contain the raw API response, not a
// trimmed projection — so we can remap without a re-fetch if we add
// columns").
public sealed class VideoCategoryListItem
{
    public string Id { get; set; } = "";
    public string? Kind { get; set; }
    public string? Etag { get; set; }
    public VideoCategorySnippetDto? Snippet { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class VideoCategorySnippetDto
{
    public string? ChannelId { get; set; }
    public string Title { get; set; } = "";
    public bool Assignable { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
