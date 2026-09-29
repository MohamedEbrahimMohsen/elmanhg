using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace Elmanhg.Api.RateLimiting;

public static class AuthRateLimiting
{
    private const string UnknownClientPartition = "unknown";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) => throw new RateLimitExceededCoreException(ErrorCodes.TooManyRequests);
        });

        services.AddOptions<RateLimiterOptions>().Configure<IOptions<AuthOptions>>((rateLimiter, authOptions) =>
        {
            var auth = authOptions.Value;
            rateLimiter.AddPolicy(AuthRateLimitPolicies.OtpRequests, httpContext => CreateFixedWindow(httpContext, auth.OtpRequestPermitLimit, auth.OtpRequestWindowSeconds));
            rateLimiter.AddPolicy(AuthRateLimitPolicies.Credentials, httpContext => CreateFixedWindow(httpContext, auth.CredentialPermitLimit, auth.CredentialWindowSeconds));
        });

        services.AddOptions<RateLimiterOptions>().Configure<IOptions<AnalyticsOptions>>((rateLimiter, analyticsOptions) => rateLimiter.AddPolicy(AnalyticsRateLimitPolicies.FunnelEvents, httpContext => CreateFixedWindow(httpContext, analyticsOptions.Value.FunnelEventPermitLimit, analyticsOptions.Value.FunnelEventWindowSeconds)));
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<ClientErrorsOptions>>((rateLimiter, clientErrorsOptions) => rateLimiter.AddPolicy(ObservabilityRateLimitPolicies.ClientErrors, httpContext => CreateFixedWindow(httpContext, clientErrorsOptions.Value.PermitLimit, clientErrorsOptions.Value.WindowSeconds)));

        return services;
    }

    private static RateLimitPartition<string> CreateFixedWindow(HttpContext httpContext, int permitLimit, int windowSeconds)
    {
        return RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownClientPartition, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0,
        });
    }
}
