using Core.DDD.Repositories;

namespace Elmanhg.Domain.EssayGrading;

public interface IEssayGradeRepository : IRepository<EssayGrade>
{
    Task<List<Guid>> GetDueIdsAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);
}
