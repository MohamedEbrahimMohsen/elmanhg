using Core.Http;
using Elmanhg.Application.Shared.AiService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.AiService;

public static class AiServiceServiceCollectionExtensions
{
    public static IServiceCollection AddAiService(this IServiceCollection services)
    {
        services.AddOptions<AiServiceOptions>().BindConfiguration(AiServiceOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<AiServiceOptions>, AiServiceOptionsValidator>();
        AddAiHttpClient<HttpAiServiceClient>(services, x => x.AttemptTimeoutSeconds, x => x.TotalTimeoutSeconds, infiniteClientTimeout: false);
        AddAiHttpClient<HttpAiTranscriptionClient>(services, x => x.TranscriptionTimeoutSeconds, x => x.TranscriptionTimeoutSeconds, infiniteClientTimeout: true);
        AddAiHttpClient<HttpAiEssayGradingClient>(services, x => x.EssayGradingTimeoutSeconds, x => x.EssayGradingTimeoutSeconds, infiniteClientTimeout: true);
        AddAiHttpClient<HttpAiMathCheckClient>(services, x => x.MathCheckTimeoutSeconds, x => x.MathCheckTimeoutSeconds, infiniteClientTimeout: true);
        AddAiHttpClient<HttpAiMathStepGradingClient>(services, x => x.MathStepGradingTimeoutSeconds, x => x.MathStepGradingTimeoutSeconds, infiniteClientTimeout: true);
        AddAiHttpClient<HttpAiConfigurationClient>(services, x => x.ConfigurationTimeoutSeconds, x => x.ConfigurationTimeoutSeconds, infiniteClientTimeout: true);
        services.AddScoped<FakeAiServiceClient>();
        services.AddScoped<FakeAiTranscriptionClient>();
        services.AddScoped<FakeAiEssayGradingClient>();
        services.AddScoped<FakeAiMathCheckClient>();
        services.AddScoped<FakeAiMathStepGradingClient>();
        services.AddSingleton<IMathCheckRateLimiter, MathCheckRateLimiter>();
        services.AddProviderSwitch<IAiServiceClient, FakeAiServiceClient, HttpAiServiceClient>(UsesHttp);
        services.AddProviderSwitch<IAiTranscriptionClient, FakeAiTranscriptionClient, HttpAiTranscriptionClient>(UsesHttp);
        services.AddProviderSwitch<IAiEssayGradingClient, FakeAiEssayGradingClient, HttpAiEssayGradingClient>(UsesHttp);
        services.AddProviderSwitch<IAiMathCheckClient, FakeAiMathCheckClient, HttpAiMathCheckClient>(UsesHttp);
        services.AddProviderSwitch<IAiMathStepGradingClient, FakeAiMathStepGradingClient, HttpAiMathStepGradingClient>(UsesHttp);
        return services;
    }

    private static void AddAiHttpClient<TClient>(IServiceCollection services, Func<AiServiceOptions, int> attemptSeconds, Func<AiServiceOptions, int> totalSeconds, bool infiniteClientTimeout)
        where TClient : class
    {
        services.AddHttpClient<TClient>((serviceProvider, client) =>
            {
                client.BaseAddress = HttpBaseAddress.From(Options(serviceProvider).BaseUrl);
                // HttpClient.Timeout wraps the resilience pipeline, so it stays infinite and the pipeline owns the budget.
                if (infiniteClientTimeout)
                {
                    client.Timeout = Timeout.InfiniteTimeSpan;
                }
            })
            .AddTimeoutResilience(serviceProvider => TimeSpan.FromSeconds(attemptSeconds(Options(serviceProvider))), serviceProvider => TimeSpan.FromSeconds(totalSeconds(Options(serviceProvider))), retryUnsafeMethods: false);
    }

    private static bool UsesHttp(IServiceProvider serviceProvider) => Options(serviceProvider).Provider switch
    {
        AiServiceProvider.Fake => false,
        AiServiceProvider.Http => true,
        _ => throw new InvalidOperationException("Unsupported AiService:Provider."),
    };

    private static AiServiceOptions Options(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<IOptions<AiServiceOptions>>().Value;
}
