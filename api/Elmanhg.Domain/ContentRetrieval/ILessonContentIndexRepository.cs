using Core.DDD.Repositories;

namespace Elmanhg.Domain.ContentRetrieval;

public interface ILessonContentIndexRepository : IRepository<LessonContentIndex>
{
    Task<List<Guid>> GetStaleLessonIdsAsync(IReadOnlyCollection<Guid> excludedIds, int batchSize, CancellationToken cancellationToken);
    Task DeleteAllAsync(CancellationToken cancellationToken);
}
