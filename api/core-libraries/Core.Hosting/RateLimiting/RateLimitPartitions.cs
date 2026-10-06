using Core.Identity.Tokens.CurrentUser;
using Microsoft.AspNetCore.Http;
using System.Threading.RateLimiting;

namespace Core.Hosting.RateLimiting;

public static class RateLimitPartitions
{
    public const string UnknownClientPartition = "unknown";

    public static RateLimitPartition<string> PerClientIp(HttpContext httpContext, int permitLimit, int windowSeconds) => FixedWindow(ClientIp(httpContext), permitLimit, windowSeconds);

    public static RateLimitPartition<string> PerUser(HttpContext httpContext, int permitLimit, int windowSeconds) => FixedWindow(UserKey(httpContext), permitLimit, windowSeconds);

    public static RateLimitPartition<string> FixedWindow(string partitionKey, int permitLimit, int windowSeconds)
    {
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0,
        });
    }

    public static string UserKey(HttpContext httpContext) => httpContext.User.FindFirst(CurrentUserService.Constants.UserIdClaimType)?.Value is { Length: > 0 } userId ? $"user:{userId}" : $"ip:{ClientIp(httpContext)}";

    public static string ClientIp(HttpContext httpContext) => httpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownClientPartition;
}
