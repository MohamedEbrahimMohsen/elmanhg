using Core.DDD.Models;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.EssayGrading;

public class EssayGradeRepository(AppDbContext context) : Repository<EssayGrade>(context), IEssayGradeRepository
{
    public async Task<List<Guid>> GetDueIdsAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(x => (x.Status == EssayGradeStatus.Pending && x.Retry.NextAttemptAt <= now) || (x.Status == EssayGradeStatus.Graded && x.AppliedAt == null))
            .OrderBy(x => x.Status == EssayGradeStatus.Pending)
            .ThenBy(x => x.Retry.NextAttemptAt)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PageData<EssayGrade>> GetInReviewPageAsync(Guid subjectId, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var query = _dbSet
            .AsNoTracking()
            .Where(x => x.SubjectId == subjectId && x.Status == EssayGradeStatus.InReview)
            .Where(x => _context.Set<Session>().Any(s => s.Id == x.SessionId && !s.IsTestMode));
        return await query.OrderBy(x => x.RequestedAt).ThenBy(x => x.Id).ToPageDataAsync(pageNumber, pageSize, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Dictionary<Guid, int>> CountInReviewBySubjectAsync(IReadOnlyCollection<Guid>? subjectIds, CancellationToken cancellationToken)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(x => (subjectIds == null || subjectIds.Contains(x.SubjectId)) && x.Status == EssayGradeStatus.InReview)
            .Where(x => _context.Set<Session>().Any(s => s.Id == x.SessionId && !s.IsTestMode))
            .GroupBy(x => x.SubjectId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken)
            .ConfigureAwait(false);
    }
}
