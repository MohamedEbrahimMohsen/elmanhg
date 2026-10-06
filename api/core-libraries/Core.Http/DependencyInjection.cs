using Microsoft.Extensions.DependencyInjection;

namespace Core.Http;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreHttp(this IServiceCollection services, string defaultUserAgent)
    {
        services.AddOptions<CoreHttpOptions>().Configure(options => options.UserAgent = defaultUserAgent).BindConfiguration(CoreHttpOptions.SectionName);
        return services;
    }
}
