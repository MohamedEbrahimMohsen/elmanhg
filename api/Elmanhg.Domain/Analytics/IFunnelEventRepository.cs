using Core.DDD.Repositories;

namespace Elmanhg.Domain.Analytics;

public interface IFunnelEventRepository : IRepository<FunnelEvent>
{
    Task<List<FunnelStepCount>> CountVisitorsByStepAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken);
    Task<FunnelTiming> GetLandingToFirstAnswerTimingAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken);
}
