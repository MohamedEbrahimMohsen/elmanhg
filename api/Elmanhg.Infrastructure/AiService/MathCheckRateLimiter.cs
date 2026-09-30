using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace Elmanhg.Infrastructure.AiService;

public sealed class MathCheckRateLimiter : IMathCheckRateLimiter, IDisposable
{
    private readonly PartitionedRateLimiter<Guid> limiter;

    public MathCheckRateLimiter(IOptions<MathStepGradingOptions> options)
    {
        var permitLimit = options.Value.CheckPermitLimit;
        var window = TimeSpan.FromSeconds(options.Value.CheckWindowSeconds);
        limiter = PartitionedRateLimiter.Create<Guid, Guid>(studentId => RateLimitPartition.GetFixedWindowLimiter(studentId, _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window, QueueLimit = 0 }));
    }

    public bool TryAcquire(Guid studentId)
    {
        using var lease = limiter.AttemptAcquire(studentId);
        return lease.IsAcquired;
    }

    public void Dispose() => limiter.Dispose();
}
