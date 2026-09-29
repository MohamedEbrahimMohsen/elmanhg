using Core.DDD.Repositories;

namespace Elmanhg.Domain.Avatar;

public interface IAvatarMessageUsageRepository : IRepository<AvatarMessageUsage>
{
    Task<int> CountOnDayAsync(Guid studentId, string timeZone, DateOnly day, CancellationToken cancellationToken);
}
