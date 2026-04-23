namespace StreamManager.Core;

// App-level settings that aren't OAuth-related: regionCode today, log
// level / window geometry later. Kept in a file separate from
// config.json so the two owners (this class and OAuthClientConfig) never
// race on concurrent writes. Same <AppData>/streammanager/ directory.
public interface IAppSettings
{
    string RegionCode { get; }

    event EventHandler? Changed;

    Task SetRegionCodeAsync(string regionCode, CancellationToken ct = default);
}
