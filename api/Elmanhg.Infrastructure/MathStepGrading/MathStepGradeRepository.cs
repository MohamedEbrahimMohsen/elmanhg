using Core.DDD.Models;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.MathStepGrading;

public class MathStepGradeRepository(AppDbContext context) : Repository<MathStepGrade>(context), IMathStepGradeRepository
{
    public async Task<List<Guid>> GetDueIdsAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(x => (x.Status == MathStepGradeStatus.Pending && x.NextAttemptAt <= now) || (x.Status == MathStepGradeStatus.Graded && x.AppliedAt == null))
            .OrderBy(x => x.Status == MathStepGradeStatus.Pending)
            .ThenBy(x => x.NextAttemptAt)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PageData<MathStepGrade>> GetInReviewPageAsync(Guid subjectId, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var query = _dbSet
            .AsNoTracking()
            .Where(x => x.SubjectId == subjectId && x.Status == MathStepGradeStatus.InReview)
            .Where(x => _context.Set<Session>().Any(s => s.Id == x.SessionId && !s.IsTestMode));
        var total = await query
            .LongCountAsync(cancellationToken)
            .ConfigureAwait(false);
        var offset = PageCalculator.Offset(pageNumber, pageSize);
        List<MathStepGrade> items = PageCalculator.IsPastEnd(offset, total) ? [] : await query
            .OrderBy(x => x.RequestedAt)
            .ThenBy(x => x.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PageData<MathStepGrade> { Items = items, PageNumber = pageNumber, PageSize = pageSize, TotalItems = total, TotalPages = PageCalculator.TotalPages(total, pageSize) };
    }

    public async Task<Dictionary<Guid, int>> CountInReviewBySubjectAsync(IReadOnlyCollection<Guid>? subjectIds, CancellationToken cancellationToken)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(x => (subjectIds == null || subjectIds.Contains(x.SubjectId)) && x.Status == MathStepGradeStatus.InReview)
            .Where(x => _context.Set<Session>().Any(s => s.Id == x.SessionId && !s.IsTestMode))
            .GroupBy(x => x.SubjectId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken)
            .ConfigureAwait(false);
    }
}
