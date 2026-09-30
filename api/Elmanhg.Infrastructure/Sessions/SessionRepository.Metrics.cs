using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Sessions;

public partial class SessionRepository
{
    public async Task<List<DailyTotal>> CountAttemptsByDayAsync(MetricsWindow window, Guid? subjectId, CancellationToken cancellationToken)
    {
        return await _context.Database
            .SqlQuery<DailyTotal>($"""
                SELECT (a."CreatedAt" AT TIME ZONE {window.TimeZone})::date AS "Day", COUNT(*)::bigint AS "Value"
                FROM "Attempts" AS a
                INNER JOIN "Sessions" AS s ON s."Id" = a."SessionId"
                INNER JOIN "Questions" AS q ON q."Id" = a."QuestionId"
                WHERE a."IsDeleted" = false AND s."IsDeleted" = false AND q."IsDeleted" = false AND s."IsTestMode" = false
                AND a."CreatedAt" >= {window.Start} AND a."CreatedAt" < {window.End} AND ({subjectId}::uuid IS NULL OR q."SubjectId" = {subjectId})
                GROUP BY 1
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<LessonAttemptOutcome>> GetAttemptOutcomesByLessonAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, decimal correctThreshold, CancellationToken cancellationToken)
    {
        return await _context.Set<Attempt>()
            .AsNoTracking()
            .Where(x => x.CreatedAt >= start && x.CreatedAt < end)
            .Join(_dbSet.Where(x => !x.IsTestMode), attempt => attempt.SessionId, session => session.Id, (attempt, session) => attempt)
            .Join(_context.Set<Question>().Where(x => subjectId == null || x.SubjectId == subjectId), attempt => attempt.QuestionId, question => question.Id, (attempt, question) => new { attempt.NormalisedScore, question.SubjectId, question.LessonId })
            .Join(_context.Set<Lesson>(), x => x.LessonId, lesson => lesson.Id, (x, lesson) => new { x.NormalisedScore, x.SubjectId, lesson.UnitId, x.LessonId })
            .GroupBy(x => new { x.SubjectId, x.UnitId, x.LessonId })
            .Select(x => new LessonAttemptOutcome(x.Key.SubjectId, x.Key.UnitId, x.Key.LessonId, x.Count(), x.Count(attempt => attempt.NormalisedScore >= correctThreshold)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
