using Core.DDD.Models;
using Core.DDD.Repositories;

namespace Elmanhg.Domain.EssayGrading;

public interface IEssayGradeRepository : IRepository<EssayGrade>
{
    Task<List<Guid>> GetDueIdsAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);
    Task<PageData<EssayGrade>> GetInReviewPageAsync(Guid subjectId, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<Dictionary<Guid, int>> CountInReviewBySubjectAsync(IReadOnlyCollection<Guid>? subjectIds, CancellationToken cancellationToken);
}
