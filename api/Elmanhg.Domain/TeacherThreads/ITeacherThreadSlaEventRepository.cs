using Core.DDD.Repositories;

namespace Elmanhg.Domain.TeacherThreads;

public interface ITeacherThreadSlaEventRepository : IRepository<TeacherThreadSlaEvent>
{
    Task<int> CountBreachesAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, CancellationToken cancellationToken);
}
