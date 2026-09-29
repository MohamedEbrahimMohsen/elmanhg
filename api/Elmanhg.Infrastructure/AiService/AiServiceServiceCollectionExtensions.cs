using Elmanhg.Application.Shared.AiService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.AiService;

public static class AiServiceServiceCollectionExtensions
{
    public static IServiceCollection AddAiService(this IServiceCollection services)
    {
        services.AddOptions<AiServiceOptions>().BindConfiguration(AiServiceOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<AiServiceOptions>, AiServiceOptionsValidator>();
        services.AddHttpClient<HttpAiServiceClient>((serviceProvider, client) => client.BaseAddress = new Uri(Options(serviceProvider).BaseUrl.TrimEnd('/') + "/"))
            .AddStandardResilienceHandler()
            .Configure((resilience, serviceProvider) =>
            {
                var aiService = Options(serviceProvider);
                resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(aiService.AttemptTimeoutSeconds);
                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(aiService.TotalTimeoutSeconds);
                // The standard handler requires the sampling window to be at least twice the attempt timeout.
                resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(aiService.AttemptTimeoutSeconds * 2);
                resilience.Retry.DisableForUnsafeHttpMethods();
            });
        services.AddScoped<FakeAiServiceClient>();
        services.AddScoped<IAiServiceClient>(serviceProvider => Options(serviceProvider).Provider switch
        {
            AiServiceProvider.Fake => serviceProvider.GetRequiredService<FakeAiServiceClient>(),
            AiServiceProvider.Http => serviceProvider.GetRequiredService<HttpAiServiceClient>(),
            _ => throw new InvalidOperationException("Unsupported AiService:Provider."),
        });
        return services;
    }

    private static AiServiceOptions Options(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<IOptions<AiServiceOptions>>().Value;
}
