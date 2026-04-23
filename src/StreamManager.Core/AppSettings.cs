using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamManager.Core;

public sealed class AppSettings : IAppSettings
{
    private const string DefaultRegionCode = "US";
    private const string FileName = "app-settings.json";

    private readonly IAppPaths _paths;
    private readonly object _lock = new();
    private string _regionCode = DefaultRegionCode;

    public AppSettings(IAppPaths paths)
    {
        _paths = paths;
        Reload();
    }

    public string RegionCode
    {
        get { lock (_lock) { return _regionCode; } }
    }

    public event EventHandler? Changed;

    public Task SetRegionCodeAsync(string regionCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(regionCode))
        {
            throw new ArgumentException("regionCode is required.", nameof(regionCode));
        }

        bool changed;
        lock (_lock)
        {
            changed = !string.Equals(
                _regionCode, regionCode, StringComparison.OrdinalIgnoreCase);
            if (changed)
            {
                _regionCode = regionCode;
                Persist();
            }
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        return Task.CompletedTask;
    }

    private void Reload()
    {
        var path = FilePath;
        if (!File.Exists(path)) return;

        try
        {
            var raw = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(raw)) return;
            var dto = JsonSerializer.Deserialize<SettingsDto>(raw, JsonOpts);
            if (dto is null) return;
            if (!string.IsNullOrWhiteSpace(dto.RegionCode))
            {
                _regionCode = dto.RegionCode;
            }
        }
        catch (JsonException)
        {
            // Malformed file: fall back to defaults. The next Save will
            // overwrite with a valid document.
        }
        catch (IOException)
        {
        }
    }

    private void Persist()
    {
        _paths.EnsureDirectoriesExist();
        var dto = new SettingsDto { RegionCode = _regionCode };
        var json = JsonSerializer.Serialize(dto, JsonOpts);
        File.WriteAllText(FilePath, json);
    }

    private string FilePath => Path.Combine(_paths.AppDataRoot, FileName);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class SettingsDto
    {
        public string? RegionCode { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }
}
