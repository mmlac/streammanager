using Microsoft.Extensions.DependencyInjection;
using StreamManager.Core.Auth;

namespace StreamManager.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStreamManagerCore(this IServiceCollection services)
    {
        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<IOAuthClientConfig, OAuthClientConfig>();
        services.AddSingleton<IBrowserLauncher, SystemBrowserLauncher>();
        services.AddSingleton<IAuthState, AuthState>();

        // HttpClient instances for the OAuth flow + userinfo. Keep them as
        // typed clients so retries/handlers can be wired in later without
        // touching the call sites.
        services.AddHttpClient<IGoogleOAuthFlow, GoogleOAuthFlow>();
        services.AddHttpClient<IUserInfoClient, UserInfoClient>();

        services.AddSingleton<IYouTubeAuthenticator, YouTubeAuthenticator>();
        services.AddSingleton<IReauthOrchestrator, ReauthOrchestrator>();
        return services;
    }
}
