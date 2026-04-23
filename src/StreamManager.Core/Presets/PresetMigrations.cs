namespace StreamManager.Core.Presets;

// Registry of version-to-version migrations applied in sequence when a
// `presets.json` envelope arrives at a version older than
// `PresetStore.CurrentSchemaVersion`.
//
// v1 is the first shipped version, so the registry is intentionally empty.
// Adding a v2 later means registering a delegate keyed by `fromVersion: 1`
// that rewrites the document — no other change to the store is required.
internal static class PresetMigrations
{
    public delegate PresetsFile Migration(PresetsFile input);

    // Keyed by the version being migrated *from*. A value at key N produces
    // the document at version N+1. All migrations are chained from the
    // on-disk version up to `CurrentSchemaVersion`.
    private static readonly IReadOnlyDictionary<int, Migration> _registry =
        new Dictionary<int, Migration>();

    public static bool TryGet(int fromVersion, out Migration migration)
    {
        if (_registry.TryGetValue(fromVersion, out var found))
        {
            migration = found;
            return true;
        }
        migration = static f => f;
        return false;
    }
}
