using Core.DDD.Repositories;

namespace Elmanhg.Domain.TeacherThreads;

public interface ITeacherThreadRepository : IRepository<TeacherThread>
{
    Task<List<Guid>> GetSlaDueIdsAsync(DateTimeOffset now, IReadOnlyCollection<Guid> excludedIds, int limit, CancellationToken cancellationToken);
    Task<List<TeacherThread>> GetRemindedOpenThreadsAsync(IReadOnlyCollection<Guid>? subjectIds, Guid callerId, int limit, CancellationToken cancellationToken);
    Task<TeacherReplyStats> GetReplyStatsAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, Guid? teacherId, CancellationToken cancellationToken);
}
