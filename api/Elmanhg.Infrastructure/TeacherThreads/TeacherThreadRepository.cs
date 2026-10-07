using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.TeacherThreads;

public class TeacherThreadRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<TeacherThread>(context, currentUser, timeProvider), ITeacherThreadRepository
{
    public async Task<List<Guid>> GetSlaDueIdsAsync(DateTimeOffset now, IReadOnlyCollection<Guid> excludedIds, int limit, CancellationToken cancellationToken)
    {
        var events = _context.Set<TeacherThreadSlaEvent>();
        return await _dbSet
            .AsNoTracking()
            .Where(x => x.Status == TeacherThreadStatus.Open && !excludedIds.Contains(x.Id) && ((x.FirstReminderDueAt <= now && !events.Any(e => e.ThreadId == x.Id && e.WindowStartedAt == x.SlaWindowStartedAt && e.Kind == TeacherThreadSlaEventKind.FirstReminder)) || (x.SecondReminderDueAt <= now && !events.Any(e => e.ThreadId == x.Id && e.WindowStartedAt == x.SlaWindowStartedAt && e.Kind == TeacherThreadSlaEventKind.SecondReminder)) || (x.SlaDueAt <= now && !events.Any(e => e.ThreadId == x.Id && e.WindowStartedAt == x.SlaWindowStartedAt && e.Kind == TeacherThreadSlaEventKind.Breach))))
            .OrderBy(x => x.SlaDueAt)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<TeacherThread>> GetRemindedOpenThreadsAsync(IReadOnlyCollection<Guid>? subjectIds, Guid callerId, int limit, CancellationToken cancellationToken)
    {
        var events = _context.Set<TeacherThreadSlaEvent>();
        return await _dbSet
            .AsNoTracking()
            .Include(x => x.Messages)
            .Where(x => x.Status == TeacherThreadStatus.Open && (x.TeacherId == callerId || (x.TeacherId == null && (subjectIds == null || subjectIds.Contains(x.SubjectId)))) && events.Any(e => e.ThreadId == x.Id && e.WindowStartedAt == x.SlaWindowStartedAt && e.Kind != TeacherThreadSlaEventKind.Breach))
            .OrderBy(x => x.SlaDueAt)
            .ThenBy(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TeacherReplyStats> GetReplyStatsAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, Guid? teacherId, CancellationToken cancellationToken)
    {
        return await _context.Database
            .SqlQuery<TeacherReplyStats>($"""
                SELECT COUNT(*)::int AS "Replies",
                COUNT(*) FILTER (WHERE NOT r."Breached")::int AS "RepliedWithinSla",
                percentile_cont(0.5) WITHIN GROUP (ORDER BY r."Wait") AS "MedianReplySeconds"
                FROM (
                    SELECT EXTRACT(EPOCH FROM (m."CreatedAt" - q."CreatedAt"))::double precision AS "Wait",
                    EXISTS (SELECT 1 FROM "TeacherThreadSlaEvents" AS e WHERE e."ThreadId" = m."ThreadId" AND e."WindowStartedAt" = q."CreatedAt" AND e."Kind" = 'Breach' AND e."IsDeleted" = false) AS "Breached"
                    FROM "TeacherMessages" AS m
                    INNER JOIN "TeacherThreads" AS t ON t."Id" = m."ThreadId"
                    INNER JOIN LATERAL (
                        SELECT s."CreatedAt"
                        FROM "TeacherMessages" AS s
                        WHERE s."ThreadId" = m."ThreadId" AND s."SenderId" = t."StudentId" AND s."CreatedAt" <= m."CreatedAt" AND s."IsDeleted" = false
                        ORDER BY s."CreatedAt" DESC
                        LIMIT 1
                    ) AS q ON true
                    WHERE m."IsDeleted" = false AND t."IsDeleted" = false AND m."SenderId" <> t."StudentId" AND m."CreatedAt" >= {start} AND m."CreatedAt" < {end}
                    AND ({subjectId}::uuid IS NULL OR t."SubjectId" = {subjectId}) AND ({teacherId}::uuid IS NULL OR m."SenderId" = {teacherId})
                ) AS r
                """)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
