using Core.DDD.Repositories;

namespace Elmanhg.Domain.Units;

public interface ICurriculumUnitRepository : IRepository<CurriculumUnit>
{
    Task<bool> AnyInSubjectAsync(Guid subjectId, CancellationToken cancellationToken);
    Task<Dictionary<Guid, int>> CountBySubjectAsync(IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken);
}
