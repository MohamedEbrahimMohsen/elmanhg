using Microsoft.Extensions.DependencyInjection;

namespace Core.Http;

public static class ProviderSwitchServiceCollectionExtensions
{
    public static IServiceCollection AddProviderSwitch<TService, TFake, TReal>(this IServiceCollection services, Func<IServiceProvider, bool> useReal)
        where TService : class
        where TFake : class, TService
        where TReal : class, TService
    {
        services.AddScoped<TService>(serviceProvider => useReal(serviceProvider) ? serviceProvider.GetRequiredService<TReal>() : serviceProvider.GetRequiredService<TFake>());
        return services;
    }

    public static IServiceCollection AddProviderSwitch<TService, TReal>(this IServiceCollection services, Func<IServiceProvider, bool> useReal, Func<IServiceProvider, TService> createFake)
        where TService : class
        where TReal : class, TService
    {
        services.AddScoped<TService>(serviceProvider => useReal(serviceProvider) ? serviceProvider.GetRequiredService<TReal>() : createFake(serviceProvider));
        return services;
    }
}
