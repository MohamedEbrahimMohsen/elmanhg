using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Sessions.Selection;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Sessions;

public partial class SessionRepository(AppDbContext context) : Repository<Session>(context), ISessionRepository
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

    public async Task<List<DateOnly>> GetQuizActivityDaysAsync(Guid studentId, string timeZone, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var kind = nameof(SessionKind.Quiz);
        return await _context.Database
            .SqlQuery<DateOnly>($"""
                SELECT DISTINCT (a."CreatedAt" AT TIME ZONE {timeZone})::date AS "Value"
                FROM "Attempts" AS a
                INNER JOIN "Sessions" AS s ON s."Id" = a."SessionId"
                WHERE a."StudentId" = {studentId} AND a."CreatedAt" >= {since} AND a."IsDeleted" = false AND s."IsDeleted" = false AND s."IsTestMode" = false AND s."Kind" = {kind}
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountQuizAttemptsOnDayAsync(Guid studentId, string timeZone, DateOnly day, CancellationToken cancellationToken)
    {
        var kind = nameof(SessionKind.Quiz);
        // Any zone's local midnight is within 14 h of UTC midnight, so the day before is a safe index bound.
        var lowerBound = new DateTimeOffset(day.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return await _context.Database
            .SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value"
                FROM "Attempts" AS a
                INNER JOIN "Sessions" AS s ON s."Id" = a."SessionId"
                WHERE a."StudentId" = {studentId} AND a."CreatedAt" >= {lowerBound} AND a."IsDeleted" = false AND s."IsDeleted" = false AND s."IsTestMode" = false AND s."Kind" = {kind}
                AND (a."CreatedAt" AT TIME ZONE {timeZone})::date = {day}
                """)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<ExamBestScore>> GetBestExamScoresAsync(Guid studentId, CancellationToken cancellationToken)
    {
        return await _dbSet
            .WhereCountsTowardBestScore(studentId)
            .GroupBy(x => x.ScopeKey)
            .Select(x => new ExamBestScore(x.Key, x.Max(session => session.ScorePercent) ?? 0m))
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<ExamAttemptSummary>> GetExamAttemptsAsync(Guid studentId, SessionKind kind, string scopeKey, CancellationToken cancellationToken)
    {
        return await _dbSet
            .WhereCountsTowardBestScore(studentId)
            .Where(x => x.Kind == kind && x.ScopeKey == scopeKey)
            .OrderByDescending(x => x.SubmittedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new ExamAttemptSummary(x.Id, x.SubmittedAt ?? DateTimeOffset.MinValue, x.ScorePercent ?? 0m))
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Attempt?> GetStudentAttemptAsync(Guid attemptId, Guid studentId, CancellationToken cancellationToken)
    {
        return await _context.Set<Attempt>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == attemptId && x.StudentId == studentId, cancellationToken)
            .ConfigureAwait(false);
    }
}
