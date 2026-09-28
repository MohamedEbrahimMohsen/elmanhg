using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Mastery;

public class QuestionMasteryRepository(AppDbContext context) : Repository<QuestionMastery>(context), IQuestionMasteryRepository
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
}
