using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Core.Http;

public static class HttpClientConsumerServiceCollectionExtensions
{
    // The HttpClient is named after the consumer, not the shared transport client, so every consumer keeps its own resilience pipeline and circuit breaker.
    public static IHttpClientBuilder AddScopedHttpConsumer<TConsumer, TClient>(this IServiceCollection services, Action<IServiceProvider, HttpClient> configureClient)
        where TConsumer : class
        where TClient : class
    {
        var name = typeof(TConsumer).Name;
        services.AddScoped(serviceProvider => ActivatorUtilities.CreateInstance<TConsumer>(serviceProvider, serviceProvider.GetRequiredService<ITypedHttpClientFactory<TClient>>().CreateClient(serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(name))));
        return services.AddHttpClient(name, configureClient);
    }
}
