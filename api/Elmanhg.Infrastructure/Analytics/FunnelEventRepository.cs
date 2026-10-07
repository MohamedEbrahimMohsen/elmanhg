using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Analytics;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Analytics;

public class FunnelEventRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<FunnelEvent>(context, currentUser, timeProvider), IFunnelEventRepository
{
    public async Task<List<FunnelStepCount>> CountVisitorsByStepAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(x => x.OccurredAt >= start && x.OccurredAt < end)
            .GroupBy(x => x.Type)
            .Select(x => new FunnelStepCount(x.Key, x.Select(funnelEvent => funnelEvent.AnonymousId).Distinct().Count()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<FunnelTiming> GetLandingToFirstAnswerTimingAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var landing = nameof(FunnelEventType.LandingViewed);
        var answered = nameof(FunnelEventType.FirstQuizAnswered);
        return await _context.Database
            .SqlQuery<FunnelTiming>($"""
                SELECT COUNT(*)::int AS "Completed",
                percentile_cont(0.5) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (v."Answered" - v."Landed"))::double precision) AS "MedianSeconds"
                FROM (
                    SELECT MIN(e."OccurredAt") FILTER (WHERE e."Type" = {landing}) AS "Landed",
                    MIN(e."OccurredAt") FILTER (WHERE e."Type" = {answered}) AS "Answered"
                    FROM "FunnelEvents" AS e
                    WHERE e."IsDeleted" = false AND e."OccurredAt" >= {start} AND e."OccurredAt" < {end}
                    GROUP BY e."AnonymousId"
                ) AS v
                WHERE v."Landed" IS NOT NULL AND v."Answered" >= v."Landed"
                """)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
