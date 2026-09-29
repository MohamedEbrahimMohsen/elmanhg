using Core.DDD.Repositories;

namespace Elmanhg.Domain.Lessons;

public interface ILessonOpeningRepository : IRepository<LessonOpening>
{
    Task<bool> IsOpenedAsync(Guid studentId, Guid lessonId, CancellationToken cancellationToken);
}
