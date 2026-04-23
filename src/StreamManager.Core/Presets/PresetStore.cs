using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamManager.Core.Presets;

public sealed class PresetStore : IPresetStore
{
    public const int CurrentSchemaVersion = 1;

    private const string TempSuffix = ".tmp";

    private readonly IAppPaths _paths;
    private readonly IAtomicWriteFaultInjector? _faultInjector;
    private readonly object _lock = new();

    public PresetStore(IAppPaths paths)
        : this(paths, faultInjector: null)
    {
    }

    // `faultInjector` exists so atomic-write tests can simulate a crash
    // between the temp-file write and the rename. Production DI always
    // passes `null`; no runtime behavior is affected when it's absent.
    internal PresetStore(IAppPaths paths, IAtomicWriteFaultInjector? faultInjector)
    {
        _paths = paths;
        _faultInjector = faultInjector;
    }

    public PresetsFile Load()
    {
        lock (_lock)
        {
            // Always sweep a stale temp file on load. A prior crash between
            // the temp write and the rename leaves one behind; leaving it
            // would mask a *later* crash during the same load/save cycle.
            TryCleanupTempFile();

            if (!File.Exists(_paths.PresetsFilePath))
            {
                return PresetsFile.Empty;
            }

            string raw;
            try
            {
                raw = File.ReadAllText(_paths.PresetsFilePath);
            }
            catch (IOException ex)
            {
                throw new PresetStoreException(
                    PresetStoreErrorCode.IoError,
                    $"Could not read presets.json at \"{_paths.PresetsFilePath}\": {ex.Message}",
                    ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new PresetStoreException(
                    PresetStoreErrorCode.IoError,
                    $"Permission denied reading presets.json at \"{_paths.PresetsFilePath}\".",
                    ex);
            }

            PresetsFileDto? dto;
            try
            {
                dto = JsonSerializer.Deserialize<PresetsFileDto>(raw, JsonOpts);
            }
            catch (JsonException ex)
            {
                throw new PresetStoreException(
                    PresetStoreErrorCode.Malformed,
                    "malformed presets.json: " + ex.Message,
                    ex);
            }

            if (dto is null)
            {
                throw new PresetStoreException(
                    PresetStoreErrorCode.Malformed,
                    "malformed presets.json: file is empty or not a JSON object.");
            }

            if (dto.SchemaVersion < 1)
            {
                throw new PresetStoreException(
                    PresetStoreErrorCode.UnknownOlder,
                    $"presets.json declares schemaVersion={dto.SchemaVersion}, " +
                    "which is older than any version this build recognizes (1).");
            }

            if (dto.SchemaVersion > CurrentSchemaVersion)
            {
                throw new PresetStoreException(
                    PresetStoreErrorCode.NewerThanSupported,
                    $"presets.json declares schemaVersion={dto.SchemaVersion}, " +
                    $"which is newer than this build's maximum ({CurrentSchemaVersion}). " +
                    "Please update StreamManager.");
            }

            var file = new PresetsFile(
                dto.SchemaVersion,
                (dto.Presets ?? Array.Empty<Preset>()).ToArray());

            // Migration chain — currently empty (§5 v1) but wired so slice
            // N+1 can land a single registry entry without touching Load.
            var v = file.SchemaVersion;
            while (v < CurrentSchemaVersion)
            {
                if (!PresetMigrations.TryGet(v, out var migrate))
                {
                    throw new PresetStoreException(
                        PresetStoreErrorCode.Malformed,
                        $"no migration registered from schemaVersion {v} to {v + 1}.");
                }
                file = migrate(file) with { SchemaVersion = v + 1 };
                v++;
            }

            return file;
        }
    }

    public void Save(PresetsFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.SchemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentException(
                $"Cannot save file at schemaVersion {file.SchemaVersion}; " +
                $"store only writes {CurrentSchemaVersion}.",
                nameof(file));
        }

        lock (_lock)
        {
            _paths.EnsureDirectoriesExist();

            var targetPath = _paths.PresetsFilePath;
            var tempPath = targetPath + TempSuffix;

            var dto = new PresetsFileDto
            {
                SchemaVersion = file.SchemaVersion,
                Presets = file.Presets.ToArray(),
            };

            var json = JsonSerializer.Serialize(dto, JsonOpts);

            try
            {
                File.WriteAllText(tempPath, json);
            }
            catch (IOException ex)
            {
                throw new PresetStoreException(
                    PresetStoreErrorCode.IoError,
                    $"Could not write temp presets file \"{tempPath}\": {ex.Message}",
                    ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new PresetStoreException(
                    PresetStoreErrorCode.IoError,
                    $"Permission denied writing temp presets file \"{tempPath}\".",
                    ex);
            }

            _faultInjector?.BeforeRename(tempPath, targetPath);

            try
            {
                // `File.Move` with overwrite:true is atomic on POSIX and
                // on NTFS (MoveFileEx REPLACE_EXISTING). That's exactly
                // the behavior design §5 asks for.
                File.Move(tempPath, targetPath, overwrite: true);
            }
            catch (IOException ex)
            {
                throw new PresetStoreException(
                    PresetStoreErrorCode.IoError,
                    $"Could not rename temp file into place: {ex.Message}",
                    ex);
            }
        }
    }

    private void TryCleanupTempFile()
    {
        var tempPath = _paths.PresetsFilePath + TempSuffix;
        if (!File.Exists(tempPath)) return;
        try
        {
            File.Delete(tempPath);
        }
        catch (IOException)
        {
            // Best-effort: if we can't clear a leftover temp file we'll
            // try again next load. Not worth aborting the whole load.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    // On-disk shape. Separate from `PresetsFile` so the public contract
    // stays immutable while deserialization can use plain setters.
    private sealed class PresetsFileDto
    {
        public int SchemaVersion { get; set; }
        public Preset[]? Presets { get; set; }
    }
}

// Test hook: injected by unit tests to simulate a crash between the temp
// write and the atomic rename. `internal` + InternalsVisibleTo keeps it
// out of the public API while still usable from the tests project.
internal interface IAtomicWriteFaultInjector
{
    void BeforeRename(string tempPath, string targetPath);
}
