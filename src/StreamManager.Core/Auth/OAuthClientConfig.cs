using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamManager.Core.Auth;

public sealed class OAuthClientConfig : IOAuthClientConfig
{
    private readonly IAppPaths _paths;
    private readonly object _lock = new();
    private OAuthClient? _current;

    public OAuthClientConfig(IAppPaths paths)
    {
        _paths = paths;
        Reload();
    }

    public bool IsConfigured => _current?.IsConfigured == true;
    public OAuthClient? Current => _current;
    public event EventHandler? Changed;

    public Task ReloadAsync(CancellationToken ct = default)
    {
        Reload();
        return Task.CompletedTask;
    }

    public Task SaveAsync(OAuthClient client, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        lock (_lock)
        {
            _paths.EnsureDirectoriesExist();
            var existing = ReadFile() ?? new ConfigFile();
            existing.OAuthClientId = client.ClientId;
            existing.OAuthClientSecret = client.ClientSecret;
            File.WriteAllText(
                _paths.ConfigFilePath,
                JsonSerializer.Serialize(existing, JsonOpts));
            _current = client;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private void Reload()
    {
        lock (_lock)
        {
            var f = ReadFile();
            if (f is null
                || string.IsNullOrWhiteSpace(f.OAuthClientId)
                || string.IsNullOrWhiteSpace(f.OAuthClientSecret))
            {
                _current = null;
                return;
            }

            _current = new OAuthClient(f.OAuthClientId, f.OAuthClientSecret);
        }
    }

    private ConfigFile? ReadFile()
    {
        if (!File.Exists(_paths.ConfigFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_paths.ConfigFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }
            return JsonSerializer.Deserialize<ConfigFile>(json, JsonOpts);
        }
        catch (JsonException)
        {
            // Malformed file: treat as "not configured" so the user can re-enter
            // values via the first-run setup screen instead of crashing the app.
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Mirrors the on-disk shape of <AppData>/streammanager/config.json. Only
    // the OAuth client fields are owned by this class; future settings (log
    // level, window geometry, etc.) get added alongside without disturbing
    // existing values because we round-trip the whole document on Save.
    private sealed class ConfigFile
    {
        public string? OAuthClientId { get; set; }
        public string? OAuthClientSecret { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }
}
