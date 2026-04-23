namespace StreamManager.Core.Presets;

public enum PresetStoreErrorCode
{
    // `presets.json` could not be parsed as JSON or did not match the
    // expected envelope shape.
    Malformed,

    // `schemaVersion` is below the earliest version this build understands
    // (e.g. 0 or negative). Treated as an unknown format — we refuse to
    // touch it rather than risk data loss.
    UnknownOlder,

    // `schemaVersion` is above what this build knows how to migrate. The
    // file was written by a newer version of StreamManager; refuse to load
    // so we don't down-convert and drop fields the newer version added.
    NewerThanSupported,

    // Low-level I/O failure unrelated to schema (permissions, disk full).
    IoError,
}
