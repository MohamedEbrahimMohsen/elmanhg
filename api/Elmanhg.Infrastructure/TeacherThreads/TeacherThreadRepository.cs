using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.TeacherThreads;

public class TeacherThreadRepository(AppDbContext context) : Repository<TeacherThread>(context), ITeacherThreadRepository
{
    public async Task<List<Guid>> GetSlaDueIdsAsync(DateTimeOffset now, TimeSpan replySla, TimeSpan firstReminderAfter, TimeSpan secondReminderAfter, IReadOnlyCollection<Guid> excludedIds, int limit, CancellationToken cancellationToken)
    {
        var firstCutoff = now + replySla - firstReminderAfter;
        var secondCutoff = now + replySla - secondReminderAfter;
        var events = _context.Set<TeacherThreadSlaEvent>();
        return await _dbSet
            .AsNoTracking()
            .Where(x => x.Status == TeacherThreadStatus.Open && !excludedIds.Contains(x.Id) && ((x.SlaDueAt <= firstCutoff && !events.Any(e => e.ThreadId == x.Id && e.SlaDueAt == x.SlaDueAt && e.Kind == TeacherThreadSlaEventKind.FirstReminder)) || (x.SlaDueAt <= secondCutoff && !events.Any(e => e.ThreadId == x.Id && e.SlaDueAt == x.SlaDueAt && e.Kind == TeacherThreadSlaEventKind.SecondReminder)) || (x.SlaDueAt <= now && !events.Any(e => e.ThreadId == x.Id && e.SlaDueAt == x.SlaDueAt && e.Kind == TeacherThreadSlaEventKind.Breach))))
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
            .Where(x => x.Status == TeacherThreadStatus.Open && (x.TeacherId == callerId || (x.TeacherId == null && (subjectIds == null || subjectIds.Contains(x.SubjectId)))) && events.Any(e => e.ThreadId == x.Id && e.SlaDueAt == x.SlaDueAt && e.Kind != TeacherThreadSlaEventKind.Breach))
            .OrderBy(x => x.SlaDueAt)
            .ThenBy(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TeacherReplyStats> GetReplyStatsAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, Guid? teacherId, TimeSpan replySla, CancellationToken cancellationToken)
    {
        var replySlaSeconds = replySla.TotalSeconds;
        return await _context.Database
            .SqlQuery<TeacherReplyStats>($"""
                SELECT COUNT(*)::int AS "Replies",
                COUNT(*) FILTER (WHERE r."Wait" <= {replySlaSeconds})::int AS "RepliedWithinSla",
                percentile_cont(0.5) WITHIN GROUP (ORDER BY r."Wait") AS "MedianReplySeconds"
                FROM (
                    SELECT EXTRACT(EPOCH FROM (m."CreatedAt" - q."CreatedAt"))::double precision AS "Wait"
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
