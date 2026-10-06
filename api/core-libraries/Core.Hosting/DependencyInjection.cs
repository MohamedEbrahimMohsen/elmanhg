using Core.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Core.Hosting;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreReverseProxy(this IServiceCollection services)
    {
        services.AddOptions<ReverseProxyOptions>().BindConfiguration(ReverseProxyOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ReverseProxyOptions>, ReverseProxyOptionsValidator>();
        return services;
    }

    // OnRejected throws, so CoreExceptionMiddleware must run before UseRateLimiter to turn it into the 429 error body.
    public static IServiceCollection AddCoreRateLimiting(this IServiceCollection services, string rejectedErrorCode)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) => throw new RateLimitExceededCoreException(rejectedErrorCode);
        });
        return services;
    }
}
