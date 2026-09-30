using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Questions;

public partial class QuestionRepository
{
    public async Task<List<QuestionInventoryCount>> CountInventoryAsync(Guid? subjectId, CancellationToken cancellationToken)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(x => subjectId == null || x.SubjectId == subjectId)
            .GroupBy(x => new { x.ValidationStatus, x.Type, IsRetired = x.RetiredAt != null })
            .Select(x => new QuestionInventoryCount(x.Key.ValidationStatus, x.Key.Type, x.Key.IsRetired, x.Count()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountServableInSubjectAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        return await _dbSet
            .WhereServable(_context.Set<Lesson>())
            .Where(x => x.SubjectId == subjectId)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<QuestionDecisionStats> GetDecisionStatsAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, Guid? teacherId, CancellationToken cancellationToken)
    {
        var approved = nameof(QuestionDecisionOutcome.Approved);
        var rejected = nameof(QuestionDecisionOutcome.Rejected);
        return await _context.Database
            .SqlQuery<QuestionDecisionStats>($"""
                SELECT COUNT(*) FILTER (WHERE d."Outcome" = {approved})::int AS "Approved",
                COUNT(*) FILTER (WHERE d."Outcome" = {rejected})::int AS "Rejected",
                percentile_cont(0.5) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (d."DecidedAt" - d."SubmittedAt"))::double precision) AS "MedianSecondsToDecision"
                FROM "QuestionDecisions" AS d
                INNER JOIN "Questions" AS q ON q."Id" = d."QuestionId"
                WHERE d."IsDeleted" = false AND q."IsDeleted" = false AND d."DecidedAt" >= {start} AND d."DecidedAt" < {end}
                AND ({subjectId}::uuid IS NULL OR q."SubjectId" = {subjectId}) AND ({teacherId}::uuid IS NULL OR d."DecidedBy" = {teacherId})
                """)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<TeacherDecisionCount>> CountDecisionsByTeacherAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, CancellationToken cancellationToken)
    {
        return await _context.Set<QuestionDecision>()
            .AsNoTracking()
            .Where(x => x.DecidedAt >= start && x.DecidedAt < end)
            .Join(_dbSet.Where(x => subjectId == null || x.SubjectId == subjectId), decision => decision.QuestionId, question => question.Id, (decision, question) => decision)
            .GroupBy(x => x.DecidedBy)
            .Select(x => new TeacherDecisionCount(x.Key, x.Count(decision => decision.Outcome == QuestionDecisionOutcome.Approved), x.Count(decision => decision.Outcome == QuestionDecisionOutcome.Rejected)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<DailyTotal>> CountDecisionsByDayAsync(MetricsWindow window, Guid? subjectId, CancellationToken cancellationToken)
    {
        return await _context.Database
            .SqlQuery<DailyTotal>($"""
                SELECT (d."DecidedAt" AT TIME ZONE {window.TimeZone})::date AS "Day", COUNT(*)::bigint AS "Value"
                FROM "QuestionDecisions" AS d
                INNER JOIN "Questions" AS q ON q."Id" = d."QuestionId"
                WHERE d."IsDeleted" = false AND q."IsDeleted" = false AND d."DecidedAt" >= {window.Start} AND d."DecidedAt" < {window.End}
                AND ({subjectId}::uuid IS NULL OR q."SubjectId" = {subjectId})
                GROUP BY 1
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
