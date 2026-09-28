using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions.Exams;
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

    public async Task<List<ServableQuestionCount>> CountServableByUnitAndTypeAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        return await _dbSet
            .WhereServable(_context.Set<Lesson>())
            .Where(x => x.SubjectId == subjectId)
            .Join(_context.Set<Lesson>(), question => question.LessonId, lesson => lesson.Id, (question, lesson) => new { lesson.UnitId, question.Type })
            .GroupBy(x => new { x.UnitId, x.Type })
            .Select(x => new ServableQuestionCount(x.Key.UnitId, x.Key.Type, x.Count()))
            .ToListAsync(cancellationToken)
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

    public async Task<List<ExamCandidate>> GetServableExamCandidatesAsync(IReadOnlyCollection<Guid> unitIds, CancellationToken cancellationToken)
    {
        return await _dbSet
            .WhereServable(_context.Set<Lesson>())
            .Join(_context.Set<Lesson>(), question => question.LessonId, lesson => lesson.Id, (question, lesson) => new { question, lesson.UnitId })
            .Where(x => unitIds.Contains(x.UnitId))
            .OrderBy(x => x.question.Id)
            .Select(x => new ExamCandidate(x.question.Id, x.question.LessonId, x.question.Type, x.question.Difficulty))
            .AsNoTracking()
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
