using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Questions;

public class QuestionRepository(AppDbContext context) : Repository<Question>(context), IQuestionRepository
{
    public async Task<bool> AnyInLessonAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        return await _dbSet.AnyAsync(x => x.LessonId == lessonId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Dictionary<Guid, int>> CountByLessonAsync(IReadOnlyCollection<Guid> lessonIds, CancellationToken cancellationToken)
    {
        return await _dbSet
            .Where(x => lessonIds.Contains(x.LessonId))
            .GroupBy(x => x.LessonId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountServableAsync(CancellationToken cancellationToken)
    {
        return await _dbSet
            .WhereServable(_context.Set<Lesson>())
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Dictionary<Guid, int>> CountServableByLessonAsync(IReadOnlyCollection<Guid> lessonIds, CancellationToken cancellationToken)
    {
        return await _dbSet
            .WhereServable(_context.Set<Lesson>())
            .Where(x => lessonIds.Contains(x.LessonId))
            .GroupBy(x => x.LessonId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<Guid>> GetServableIdsInLessonAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        return await _dbSet
            .WhereServable(_context.Set<Lesson>())
            .Where(x => x.LessonId == lessonId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<QuestionRevision>> GetRevisionsAsync(IReadOnlyCollection<Guid> questionIds, CancellationToken cancellationToken)
    {
        return await _context.Set<QuestionRevision>()
            .Where(x => questionIds.Contains(x.QuestionId))
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
