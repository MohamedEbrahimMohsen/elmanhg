using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Core.Http;

public static class HttpResilienceExtensions
{
    public static IHttpStandardResiliencePipelineBuilder AddTimeoutResilience(this IHttpClientBuilder builder, Func<IServiceProvider, TimeSpan> attemptTimeout, Func<IServiceProvider, TimeSpan> totalTimeout, bool retryUnsafeMethods) => builder.AddStandardResilienceHandler().Configure((resilience, serviceProvider) =>
    {
        var attempt = attemptTimeout(serviceProvider);
        resilience.AttemptTimeout.Timeout = attempt;
        resilience.TotalRequestTimeout.Timeout = totalTimeout(serviceProvider);
        // The standard handler requires the circuit breaker sampling window to be at least twice the attempt timeout.
        if (resilience.CircuitBreaker.SamplingDuration < attempt * 2)
        {
            resilience.CircuitBreaker.SamplingDuration = attempt * 2;
        }

        if (!retryUnsafeMethods)
        {
            resilience.Retry.DisableForUnsafeHttpMethods();
        }
    });
}
