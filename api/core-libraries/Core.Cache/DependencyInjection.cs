using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Cache;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreCache(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddOptions<CachingOptions>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehaviour<,>));
        return services;
    }
}
