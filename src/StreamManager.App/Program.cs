using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using StreamManager.App.Auth;
using StreamManager.App.Services;
using StreamManager.App.ViewModels;
using StreamManager.Core;
using StreamManager.Core.Auth;

namespace StreamManager.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        using var host = CreateHostBuilder(args).Build();

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
                RegisterPlatformTokenStore(services);
                services.AddSingleton<IReauthPrompt, UiReauthPrompt>();
                services.AddSingleton<IConfirmOverwritePrompt, UiConfirmOverwritePrompt>();
                services.AddSingleton<IDirtyFormGuard, StreamFormDirtyFormGuard>();
                services.AddSingleton<IStreamFetchCoordinator, StreamFetchCoordinator>();
                services.AddSingleton<IUnreachableThumbnailPrompt, UiUnreachableThumbnailPrompt>();
                services.AddSingleton<IThumbnailFileChecker, FileSystemThumbnailChecker>();
                services.AddSingleton<IApplyOrchestrator, ApplyOrchestrator>();
                services.AddSingleton<Presets.IPresetDialogs, Presets.AvaloniaPresetDialogs>();
                services.AddSingleton<TimeProvider>(TimeProvider.System);
                services.AddSingleton(sp =>
                    (IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current!.ApplicationLifetime!);
                // Lazy variant for consumers (reauth prompt, confirm-overwrite
                // prompt) that need to stay constructible in test hosts where
                // Avalonia.Application.Current is null.
                services.AddSingleton(sp => new Lazy<IClassicDesktopStyleApplicationLifetime>(
                    () => (IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current!.ApplicationLifetime!));
                services.AddTransient<FirstRunSetupViewModel>();
                services.AddSingleton<ConnectAccountViewModel>();
                services.AddSingleton<StreamFormViewModel>();
                services.AddSingleton<PresetActionsViewModel>();
                services.AddSingleton<MainWindowViewModel>();
            });

    // Bind ITokenStore to the appropriate platform implementation. Each
    // platform project carries native-API code (Keychain Services / Credential
    // Manager) that only makes sense on its target OS, so we never register
    // the wrong one.
    private static void RegisterPlatformTokenStore(IServiceCollection services)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<ITokenStore, Platform.Windows.WindowsTokenStore>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddSingleton<ITokenStore, Platform.Mac.MacTokenStore>();
        }
        else
        {
            // Linux / unsupported: keep DI satisfied so dev builds don't crash,
            // but route to a no-op that will surface an error if used.
            services.AddSingleton<ITokenStore, UnsupportedPlatformTokenStore>();
        }
    }

    // Slot used on platforms outside the design's supported targets (Windows,
    // macOS) to keep DI complete during development. Any actual call throws
    // so failures are loud.
    private sealed class UnsupportedPlatformTokenStore : ITokenStore
    {
        private static InvalidOperationException Fail() =>
            new("Token storage is not implemented on this platform; supported targets are Windows and macOS.");

        public Task<string?> GetRefreshTokenAsync(CancellationToken ct = default) => throw Fail();
        public Task SetRefreshTokenAsync(string refreshToken, CancellationToken ct = default) => throw Fail();
        public Task DeleteRefreshTokenAsync(CancellationToken ct = default) => throw Fail();
    }
}
