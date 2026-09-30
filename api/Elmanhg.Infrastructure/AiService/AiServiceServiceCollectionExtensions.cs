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
        services.AddHttpClient<HttpAiTranscriptionClient>((serviceProvider, client) =>
            {
                client.BaseAddress = new Uri(Options(serviceProvider).BaseUrl.TrimEnd('/') + "/");
                // HttpClient.Timeout wraps the resilience pipeline, so it stays infinite and the pipeline owns the transcription budget.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddStandardResilienceHandler()
            .Configure((resilience, serviceProvider) =>
            {
                var timeout = TimeSpan.FromSeconds(Options(serviceProvider).TranscriptionTimeoutSeconds);
                resilience.AttemptTimeout.Timeout = timeout;
                resilience.TotalRequestTimeout.Timeout = timeout;
                resilience.CircuitBreaker.SamplingDuration = timeout * 2;
                resilience.Retry.DisableForUnsafeHttpMethods();
            });
        services.AddHttpClient<HttpAiEssayGradingClient>((serviceProvider, client) =>
            {
                client.BaseAddress = new Uri(Options(serviceProvider).BaseUrl.TrimEnd('/') + "/");
                // HttpClient.Timeout wraps the resilience pipeline, so it stays infinite and the pipeline owns the grading budget.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddStandardResilienceHandler()
            .Configure((resilience, serviceProvider) =>
            {
                var timeout = TimeSpan.FromSeconds(Options(serviceProvider).EssayGradingTimeoutSeconds);
                resilience.AttemptTimeout.Timeout = timeout;
                resilience.TotalRequestTimeout.Timeout = timeout;
                resilience.CircuitBreaker.SamplingDuration = timeout * 2;
                resilience.Retry.DisableForUnsafeHttpMethods();
            });
        services.AddHttpClient<HttpAiMathCheckClient>((serviceProvider, client) =>
            {
                client.BaseAddress = new Uri(Options(serviceProvider).BaseUrl.TrimEnd('/') + "/");
                // HttpClient.Timeout wraps the resilience pipeline, so it stays infinite and the pipeline owns the math check budget.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddStandardResilienceHandler()
            .Configure((resilience, serviceProvider) =>
            {
                var timeout = TimeSpan.FromSeconds(Options(serviceProvider).MathCheckTimeoutSeconds);
                resilience.AttemptTimeout.Timeout = timeout;
                resilience.TotalRequestTimeout.Timeout = timeout;
                resilience.CircuitBreaker.SamplingDuration = timeout * 2;
                resilience.Retry.DisableForUnsafeHttpMethods();
            });
        services.AddHttpClient<HttpAiMathStepGradingClient>((serviceProvider, client) =>
            {
                client.BaseAddress = new Uri(Options(serviceProvider).BaseUrl.TrimEnd('/') + "/");
                // HttpClient.Timeout wraps the resilience pipeline, so it stays infinite and the pipeline owns the grading budget.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddStandardResilienceHandler()
            .Configure((resilience, serviceProvider) =>
            {
                var timeout = TimeSpan.FromSeconds(Options(serviceProvider).MathStepGradingTimeoutSeconds);
                resilience.AttemptTimeout.Timeout = timeout;
                resilience.TotalRequestTimeout.Timeout = timeout;
                resilience.CircuitBreaker.SamplingDuration = timeout * 2;
                resilience.Retry.DisableForUnsafeHttpMethods();
            });
        services.AddScoped<FakeAiServiceClient>();
        services.AddScoped<FakeAiTranscriptionClient>();
        services.AddScoped<FakeAiEssayGradingClient>();
        services.AddScoped<FakeAiMathCheckClient>();
        services.AddScoped<FakeAiMathStepGradingClient>();
        services.AddSingleton<IMathCheckRateLimiter, MathCheckRateLimiter>();
        services.AddScoped<IAiServiceClient>(serviceProvider => Options(serviceProvider).Provider switch
        {
            AiServiceProvider.Fake => serviceProvider.GetRequiredService<FakeAiServiceClient>(),
            AiServiceProvider.Http => serviceProvider.GetRequiredService<HttpAiServiceClient>(),
            _ => throw new InvalidOperationException("Unsupported AiService:Provider."),
        });
        services.AddScoped<IAiTranscriptionClient>(serviceProvider => Options(serviceProvider).Provider switch
        {
            AiServiceProvider.Fake => serviceProvider.GetRequiredService<FakeAiTranscriptionClient>(),
            AiServiceProvider.Http => serviceProvider.GetRequiredService<HttpAiTranscriptionClient>(),
            _ => throw new InvalidOperationException("Unsupported AiService:Provider."),
        });
        services.AddScoped<IAiEssayGradingClient>(serviceProvider => Options(serviceProvider).Provider switch
        {
            AiServiceProvider.Fake => serviceProvider.GetRequiredService<FakeAiEssayGradingClient>(),
            AiServiceProvider.Http => serviceProvider.GetRequiredService<HttpAiEssayGradingClient>(),
            _ => throw new InvalidOperationException("Unsupported AiService:Provider."),
        });
        services.AddScoped<IAiMathCheckClient>(serviceProvider => Options(serviceProvider).Provider switch
        {
            AiServiceProvider.Fake => serviceProvider.GetRequiredService<FakeAiMathCheckClient>(),
            AiServiceProvider.Http => serviceProvider.GetRequiredService<HttpAiMathCheckClient>(),
            _ => throw new InvalidOperationException("Unsupported AiService:Provider."),
        });
        services.AddScoped<IAiMathStepGradingClient>(serviceProvider => Options(serviceProvider).Provider switch
        {
            AiServiceProvider.Fake => serviceProvider.GetRequiredService<FakeAiMathStepGradingClient>(),
            AiServiceProvider.Http => serviceProvider.GetRequiredService<HttpAiMathStepGradingClient>(),
            _ => throw new InvalidOperationException("Unsupported AiService:Provider."),
        });
        return services;
    }

    private static AiServiceOptions Options(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<IOptions<AiServiceOptions>>().Value;
}
