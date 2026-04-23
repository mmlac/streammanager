using Microsoft.Extensions.DependencyInjection;
using StreamManager.Core.Auth;
using StreamManager.Core.Presets;
using StreamManager.Core.Youtube;

namespace StreamManager.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStreamManagerCore(this IServiceCollection services)
    {
        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<IOAuthClientConfig, OAuthClientConfig>();
        services.AddSingleton<IBrowserLauncher, SystemBrowserLauncher>();
        services.AddSingleton<IAuthState, AuthState>();
        services.AddSingleton<IPresetStore, PresetStore>();

        // HttpClient instances for the OAuth flow + userinfo. Keep them as
        // typed clients so retries/handlers can be wired in later without
        // touching the call sites.
        services.AddHttpClient<IGoogleOAuthFlow, GoogleOAuthFlow>();
        services.AddHttpClient<IUserInfoClient, UserInfoClient>();

        services.AddSingleton<IYouTubeAuthenticator, YouTubeAuthenticator>();
        services.AddSingleton<IReauthOrchestrator, ReauthOrchestrator>();
        services.AddSingleton<IYouTubeClient, YouTubeClient>();
        // Slice 5 stub — replaced with the real Google.Apis upload in slice 8.
        services.AddSingleton<IThumbnailUploader, StubThumbnailUploader>();
        return services;
    }
}
