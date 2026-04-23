using Microsoft.Extensions.DependencyInjection;

namespace StreamManager.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStreamManagerCore(this IServiceCollection services)
    {
        services.AddSingleton<IAppPaths, AppPaths>();
        return services;
    }
}
