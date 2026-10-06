using Core.Hosting.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Elmanhg.Api.RateLimiting;

public static class StudentConcurrencyPartitions
{
    private static readonly HashSet<string> ConcurrencyCappedPolicies = [StudentRateLimitPolicies.AvatarMessages, StudentRateLimitPolicies.AskTeacherSubmissions];

    public static RateLimitPartition<string> ConcurrentStudentRequests(HttpContext httpContext, int concurrencyLimit)
    {
        var policy = httpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        if (policy is null || !ConcurrencyCappedPolicies.Contains(policy))
        {
            return RateLimitPartition.GetNoLimiter(string.Empty);
        }

        return RateLimitPartition.GetConcurrencyLimiter($"{policy}|{RateLimitPartitions.UserKey(httpContext)}", _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = concurrencyLimit,
            QueueLimit = 0,
        });
    }
}
