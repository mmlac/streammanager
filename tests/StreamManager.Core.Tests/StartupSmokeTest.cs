using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using StreamManager.App;
using StreamManager.App.ViewModels;
using StreamManager.Core;
using Xunit;

namespace StreamManager.Core.Tests;

public class StartupSmokeTest
{
    [Fact]
    public void Host_Builds_And_Resolves_MainWindowViewModel()
    {
        using var tempRoot = new TempAppDataRoot();
        using var host = BuildHost(tempRoot.Path);

        var vm = host.Services.GetRequiredService<MainWindowViewModel>();
        Assert.NotNull(vm);
    }

    [Fact]
    public void Logger_Writes_To_Configured_File_Path()
    {
        using var tempRoot = new TempAppDataRoot();

        using (var host = BuildHost(tempRoot.Path))
        {
            var paths = host.Services.GetRequiredService<IAppPaths>();
            paths.EnsureDirectoriesExist();

            var logger = host.Services.GetRequiredService<ILogger<StartupSmokeTest>>();
            logger.LogInformation("smoke test startup line");

            Log.CloseAndFlush();

            var logFiles = Directory.GetFiles(paths.LogsDirectory, "streammanager-*.log");
            Assert.NotEmpty(logFiles);
            var content = File.ReadAllText(logFiles[0]);
            Assert.Contains("smoke test startup line", content);
        }
    }

    private static Microsoft.Extensions.Hosting.IHost BuildHost(string appDataRoot)
        => Program.CreateHostBuilder(Array.Empty<string>())
            .ConfigureServices((_, services) =>
            {
                // Replace the default IAppPaths so tests write under a temp
                // directory instead of the user's real app-data.
                services.AddSingleton<IAppPaths>(new AppPaths(appDataRoot));
            })
            .Build();

    private sealed class TempAppDataRoot : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"sm-smoke-{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                try { Directory.Delete(Path, recursive: true); }
                catch { /* best effort */ }
            }
        }
    }
}
