using Core.Hosting;
using Core.Hosting.RateLimiting;
using Core.Utilities;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace Elmanhg.Api.RateLimiting;

public static class AppRateLimiting
{
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddValidatedOptions<RateLimitingOptions>(RateLimitingOptions.SectionName);

        services.AddCoreRateLimiting(ErrorCodes.TooManyRequests);

        services.AddOptions<RateLimiterOptions>().Configure<IOptions<AuthOptions>>((rateLimiter, authOptions) =>
        {
            var auth = authOptions.Value;
            rateLimiter.AddPolicy(AuthRateLimitPolicies.OtpRequests, httpContext => RateLimitPartitions.PerClientIp(httpContext, auth.OtpRequestPermitLimit, auth.OtpRequestWindowSeconds));
            rateLimiter.AddPolicy(AuthRateLimitPolicies.Credentials, httpContext => RateLimitPartitions.PerClientIp(httpContext, auth.CredentialPermitLimit, auth.CredentialWindowSeconds));
        });

        services.AddOptions<RateLimiterOptions>().Configure<IOptions<AnalyticsOptions>>((rateLimiter, analyticsOptions) => rateLimiter.AddPolicy(AnalyticsRateLimitPolicies.FunnelEvents, httpContext => RateLimitPartitions.PerClientIp(httpContext, analyticsOptions.Value.FunnelEventPermitLimit, analyticsOptions.Value.FunnelEventWindowSeconds)));
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<ClientErrorsOptions>>((rateLimiter, clientErrorsOptions) => rateLimiter.AddPolicy(ObservabilityRateLimitPolicies.ClientErrors, httpContext => RateLimitPartitions.PerClientIp(httpContext, clientErrorsOptions.Value.PermitLimit, clientErrorsOptions.Value.WindowSeconds)));

        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitingOptions>>((rateLimiter, rateLimitingOptions) =>
        {
            var limits = rateLimitingOptions.Value;
            rateLimiter.AddPolicy(AuthRateLimitPolicies.Refresh, httpContext => RateLimitPartitions.PerClientIp(httpContext, limits.AuthRefreshPermitLimit, limits.AuthRefreshWindowSeconds));
            rateLimiter.AddPolicy(PublicRateLimitPolicies.Reads, httpContext => RateLimitPartitions.PerClientIp(httpContext, limits.PublicReadPermitLimit, limits.PublicReadWindowSeconds));
            rateLimiter.AddPolicy(PublicRateLimitPolicies.PaymentWebhooks, httpContext => RateLimitPartitions.PerClientIp(httpContext, limits.PaymentWebhookPermitLimit, limits.PaymentWebhookWindowSeconds));
            rateLimiter.AddPolicy(StudentRateLimitPolicies.AvatarMessages, httpContext => RateLimitPartitions.PerUser(httpContext, limits.AvatarMessagePermitLimit, limits.AvatarMessageWindowSeconds));
            rateLimiter.AddPolicy(StudentRateLimitPolicies.AskTeacherSubmissions, httpContext => RateLimitPartitions.PerUser(httpContext, limits.AskTeacherSubmissionPermitLimit, limits.AskTeacherSubmissionWindowSeconds));
            rateLimiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext => StudentConcurrencyPartitions.ConcurrentStudentRequests(httpContext, limits.StudentConcurrentRequestLimit));
        });

        return services;
    }
}
