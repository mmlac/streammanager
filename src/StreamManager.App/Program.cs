using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using StreamManager.App.ViewModels;
using StreamManager.Core;

namespace StreamManager.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Build the host (DI + logging + app paths) before Avalonia so the
        // log sink is ready for the very first startup line.
        using var host = CreateHostBuilder(args).Build();

        // Make sure app-data and log directories exist on first launch
        // (before Serilog starts writing).
        host.Services.GetRequiredService<IAppPaths>().EnsureDirectoriesExist();

        var logger = host.Services.GetRequiredService<ILogger<Application>>();
        logger.LogInformation("StreamManager starting");

        try
        {
            AppHost.Services = host.Services;
            return BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            logger.LogInformation("StreamManager shutting down");
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog((ctx, services, cfg) =>
            {
                var paths = services.GetRequiredService<IAppPaths>();
                paths.EnsureDirectoriesExist();
                // Per acceptance criteria the file name is streammanager-YYYY-MM-DD.log.
                // Serilog's built-in rolling interval uses yyyyMMdd with no separators,
                // so we bake today's date into the path explicitly.
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var logPath = Path.Combine(paths.LogsDirectory, $"streammanager-{today}.log");
                cfg
                    .MinimumLevel.Information()
                    .WriteTo.File(
                        logPath,
                        shared: true,
                        flushToDiskInterval: TimeSpan.FromSeconds(1));
            })
            .ConfigureServices((_, services) =>
            {
                services.AddStreamManagerCore();
                services.AddSingleton<MainWindowViewModel>();
            });
}
