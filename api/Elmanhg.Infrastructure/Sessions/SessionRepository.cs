using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Sessions.Selection;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Sessions;

public class SessionRepository(AppDbContext context) : Repository<Session>(context), ISessionRepository
{
    public async Task<List<QuestionAttemptSummary>> GetAttemptSummariesAsync(Guid studentId, IReadOnlyCollection<Guid> questionIds, decimal correctThreshold, CancellationToken cancellationToken)
    {
        return await _context.Set<Attempt>()
            .Where(x => x.StudentId == studentId && questionIds.Contains(x.QuestionId))
            .GroupBy(x => x.QuestionId)
            .Select(x => new QuestionAttemptSummary(x.Key, x.Count(), x.Count(attempt => attempt.NormalisedScore >= correctThreshold), x.Max(attempt => attempt.CreatedAt), x.Max(attempt => attempt.NormalisedScore >= correctThreshold ? attempt.CreatedAt : (DateTimeOffset?)null)))
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
