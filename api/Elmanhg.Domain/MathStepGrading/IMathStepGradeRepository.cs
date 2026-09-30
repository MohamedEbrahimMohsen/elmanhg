using Core.DDD.Repositories;

namespace Elmanhg.Domain.MathStepGrading;

public interface IMathStepGradeRepository : IRepository<MathStepGrade>
{
    Task<List<Guid>> GetDueIdsAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);
}
