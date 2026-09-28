using Core.DDD.Repositories;
using Elmanhg.Domain.Sessions.Selection;

namespace Elmanhg.Domain.Sessions;

public interface ISessionRepository : IRepository<Session>
{
    Task<List<QuestionAttemptSummary>> GetAttemptSummariesAsync(Guid studentId, IReadOnlyCollection<Guid> questionIds, decimal correctThreshold, CancellationToken cancellationToken);
}
