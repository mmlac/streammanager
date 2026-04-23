using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamManager.Core.Youtube;

// Cache row for a single item in the `i18nLanguages.list` response.
// See VideoCategoryListItem for the rationale behind JsonExtensionData.
public sealed class I18nLanguageListItem
{
    public string Id { get; set; } = "";
    public string? Kind { get; set; }
    public string? Etag { get; set; }
    public I18nLanguageSnippetDto? Snippet { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class I18nLanguageSnippetDto
{
    public string Hl { get; set; } = "";
    public string Name { get; set; } = "";

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
