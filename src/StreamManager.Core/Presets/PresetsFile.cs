namespace StreamManager.Core.Presets;

// Envelope for `presets.json`. Versioned at the file level (design §5) so
// migrations are scoped to the whole document, not individual preset entries.
public sealed record PresetsFile(
    int SchemaVersion,
    IReadOnlyList<Preset> Presets)
{
    public static PresetsFile Empty { get; } =
        new(PresetStore.CurrentSchemaVersion, Array.Empty<Preset>());
}
