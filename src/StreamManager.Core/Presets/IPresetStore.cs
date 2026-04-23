namespace StreamManager.Core.Presets;

public interface IPresetStore
{
    // Reads `<AppData>/streammanager/presets.json` and returns its contents.
    // Returns `PresetsFile.Empty` when the file does not exist (first run).
    // Throws `PresetStoreException` with a populated error code for any
    // malformed or unsupported-schema input — callers surface that to the
    // user rather than silently dropping state (design §5).
    PresetsFile Load();

    // Atomically overwrites the on-disk file with `file`. Writes to a
    // sibling temp file and renames into place so a crash mid-write leaves
    // the previous contents intact.
    void Save(PresetsFile file);
}
