namespace StreamManager.Core;

public interface IAppPaths
{
    string AppDataRoot { get; }
    string LogsDirectory { get; }
    string ConfigFilePath { get; }
    string PresetsFilePath { get; }
    string CacheDirectory { get; }

    void EnsureDirectoriesExist();
}
