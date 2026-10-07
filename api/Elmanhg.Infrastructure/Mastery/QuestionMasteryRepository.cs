using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Mastery;

public class QuestionMasteryRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<QuestionMastery>(context, currentUser, timeProvider), IQuestionMasteryRepository
{
    public async Task<List<LessonMasteryCount>> GetLessonCountsAsync(Guid studentId, Guid? subjectId, CancellationToken cancellationToken)
    {
        var servable = _context.Set<Question>().WhereServable(_context.Set<Lesson>());
        var rows = from question in servable
                   join lesson in _context.Set<Lesson>() on question.LessonId equals lesson.Id
                   join unit in _context.Set<CurriculumUnit>() on lesson.UnitId equals unit.Id
                   join subject in _context.Set<Subject>() on unit.SubjectId equals subject.Id
                   where subjectId == null || unit.SubjectId == subjectId
                   from mastery in _dbSet.Where(x => x.StudentId == studentId && x.QuestionId == question.Id).DefaultIfEmpty()
                   select new { unit.SubjectId, SubjectOrder = subject.Order, lesson.UnitId, UnitOrder = unit.Order, LessonId = lesson.Id, LessonOrder = lesson.Order, Seen = mastery != null ? 1 : 0, Mastered = mastery != null && mastery.IsMastered ? 1 : 0 };
        return await rows
            .GroupBy(x => new { x.SubjectId, x.SubjectOrder, x.UnitId, x.UnitOrder, x.LessonId, x.LessonOrder })
            .Select(x => new LessonMasteryCount(x.Key.SubjectId, x.Key.SubjectOrder, x.Key.UnitId, x.Key.UnitOrder, x.Key.LessonId, x.Key.LessonOrder, x.Count(), x.Sum(row => row.Mastered), x.Sum(row => row.Seen)))
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<ObjectiveMasteryCount>> GetObjectiveCountsAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var servable = _context.Set<Question>().WhereServable(_context.Set<Lesson>());
        var rows = from question in servable
                   join objective in _context.Set<LessonObjective>() on question.ObjectiveId equals (Guid?)objective.Id
                   join lesson in _context.Set<Lesson>() on question.LessonId equals lesson.Id
                   join unit in _context.Set<CurriculumUnit>() on lesson.UnitId equals unit.Id
                   join subject in _context.Set<Subject>() on unit.SubjectId equals subject.Id
                   from mastery in _dbSet.Where(x => x.StudentId == studentId && x.QuestionId == question.Id).DefaultIfEmpty()
                   select new { unit.SubjectId, SubjectOrder = subject.Order, lesson.UnitId, UnitOrder = unit.Order, LessonId = lesson.Id, LessonOrder = lesson.Order, ObjectiveId = objective.Id, ObjectiveOrder = objective.Order, Seen = mastery != null ? 1 : 0, Mastered = mastery != null && mastery.IsMastered ? 1 : 0 };
        return await rows
            .GroupBy(x => new { x.SubjectId, x.SubjectOrder, x.UnitId, x.UnitOrder, x.LessonId, x.LessonOrder, x.ObjectiveId, x.ObjectiveOrder })
            .Select(x => new ObjectiveMasteryCount(x.Key.SubjectId, x.Key.SubjectOrder, x.Key.UnitId, x.Key.UnitOrder, x.Key.LessonId, x.Key.LessonOrder, x.Key.ObjectiveId, x.Key.ObjectiveOrder, x.Count(), x.Sum(row => row.Mastered), x.Sum(row => row.Seen)))
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
