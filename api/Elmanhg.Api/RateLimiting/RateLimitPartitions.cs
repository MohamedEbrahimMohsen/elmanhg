using Core.Identity.Tokens.CurrentUser;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Elmanhg.Api.RateLimiting;

public static class RateLimitPartitions
{
    private const string UnknownClientPartition = "unknown";

    private static readonly HashSet<string> ConcurrencyCappedPolicies = [StudentRateLimitPolicies.AvatarMessages, StudentRateLimitPolicies.AskTeacherSubmissions];

    public static RateLimitPartition<string> PerClientIp(HttpContext httpContext, int permitLimit, int windowSeconds) => FixedWindow(ClientIp(httpContext), permitLimit, windowSeconds);

    public static RateLimitPartition<string> PerUser(HttpContext httpContext, int permitLimit, int windowSeconds) => FixedWindow(UserKey(httpContext), permitLimit, windowSeconds);

    public static RateLimitPartition<string> ConcurrentStudentRequests(HttpContext httpContext, int concurrencyLimit)
    {
        var policy = httpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        if (policy is null || !ConcurrencyCappedPolicies.Contains(policy))
        {
            return RateLimitPartition.GetNoLimiter(string.Empty);
        }

        return RateLimitPartition.GetConcurrencyLimiter($"{policy}|{UserKey(httpContext)}", _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = concurrencyLimit,
            QueueLimit = 0,
        });
    }

    private static RateLimitPartition<string> FixedWindow(string partitionKey, int permitLimit, int windowSeconds)
    {
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0,
        });
    }

    private static string UserKey(HttpContext httpContext) => httpContext.User.FindFirst(CurrentUserService.Constants.UserIdClaimType)?.Value is { Length: > 0 } userId ? $"user:{userId}" : $"ip:{ClientIp(httpContext)}";

    private static string ClientIp(HttpContext httpContext) => httpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownClientPartition;
}
