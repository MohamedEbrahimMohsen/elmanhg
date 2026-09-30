using Core.DDD.Repositories;
using Elmanhg.Domain.SharedKernel;

namespace Elmanhg.Domain.Analytics;

public interface IUserActivityDayRepository : IRepository<UserActivityDay>
{
    Task AddIfAbsentAsync(UserActivityDay activity, CancellationToken cancellationToken);
    Task<int> CountActiveStudentsAsync(DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken);
    Task<List<DailyTotal>> CountActiveStudentsByDayAsync(DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken);
}
