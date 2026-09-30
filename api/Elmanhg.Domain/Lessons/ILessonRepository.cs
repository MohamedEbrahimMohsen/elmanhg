using Core.DDD.Repositories;

namespace Elmanhg.Domain.Lessons;

public interface ILessonRepository : IRepository<Lesson>
{
    Task<Lesson?> GetWithObjectivesAsync(Guid lessonId, bool asNoTracking, CancellationToken cancellationToken);
    Task<bool> AnyInUnitAsync(Guid unitId, CancellationToken cancellationToken);
    Task<Dictionary<Guid, int>> CountByUnitAsync(IReadOnlyCollection<Guid> unitIds, bool publishedOnly, CancellationToken cancellationToken);
    Task<List<LessonPosition>> GetPublishedPositionsAsync(CancellationToken cancellationToken);
    Task<List<LessonPosition>> GetPublishedSiblingPositionsAsync(Guid lessonId, CancellationToken cancellationToken);
    Task<List<LessonStateCount>> CountByStateAsync(Guid? subjectId, CancellationToken cancellationToken);
}
