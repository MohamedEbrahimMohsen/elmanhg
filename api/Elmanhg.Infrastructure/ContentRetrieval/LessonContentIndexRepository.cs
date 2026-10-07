using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.ContentRetrieval;

public class LessonContentIndexRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<LessonContentIndex>(context, currentUser, timeProvider), ILessonContentIndexRepository
{
    public async Task<List<Guid>> GetStaleLessonIdsAsync(IReadOnlyCollection<Guid> excludedIds, int batchSize, CancellationToken cancellationToken)
    {
        var lessons = _context.Set<Lesson>();
        var published = lessons.Where(ServableQuestionSpecification.LessonCondition);
        var servable = _context.Set<Question>().WhereServable(lessons);
        var outdated = published
            .Where(lesson => !_dbSet.Any(index => index.LessonId == lesson.Id && index.SourceUpdatedAt == lesson.UpdationDate && index.QuestionsUpdatedAt == servable.Where(question => question.LessonId == lesson.Id).Max(question => (DateTimeOffset?)question.UpdationDate)))
            .Select(lesson => new { LessonId = lesson.Id, ChangedAt = lesson.UpdationDate });
        var withdrawn = _dbSet
            .Where(index => !published.Any(lesson => lesson.Id == index.LessonId))
            .Select(index => new { index.LessonId, ChangedAt = index.IndexedAt });
        return await outdated
            .Concat(withdrawn)
            .Where(x => !excludedIds.Contains(x.LessonId))
            .OrderBy(x => x.ChangedAt)
            .ThenBy(x => x.LessonId)
            .Select(x => x.LessonId)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken)
    {
        // Derived, non-audited index state; the rebuild command itself is the audited action, and the sweep re-creates every row.
        await _dbSet.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
