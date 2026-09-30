using Core.DDD.Models;
using Core.DDD.Repositories;

namespace Elmanhg.Domain.MathStepGrading;

public interface IMathStepGradeRepository : IRepository<MathStepGrade>
{
    Task<List<Guid>> GetDueIdsAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);
    Task<PageData<MathStepGrade>> GetInReviewPageAsync(Guid subjectId, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<Dictionary<Guid, int>> CountInReviewBySubjectAsync(IReadOnlyCollection<Guid>? subjectIds, CancellationToken cancellationToken);
}
