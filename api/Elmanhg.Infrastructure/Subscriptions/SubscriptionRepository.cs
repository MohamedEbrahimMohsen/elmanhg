using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Subscriptions;

public class SubscriptionRepository(AppDbContext context) : Repository<Subscription>(context), ISubscriptionRepository
{
    public async Task<List<PlanCount>> CountActiveByPlanAsync(CancellationToken cancellationToken)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(x => x.Status == SubscriptionStatus.Active || x.Status == SubscriptionStatus.PastDue)
            .GroupBy(x => x.Plan)
            .Select(x => new PlanCount(x.Key, x.Count()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<long> GetMonthlyRecurringRevenueMinorAsync(CancellationToken cancellationToken)
    {
        var succeeded = nameof(PaymentStatus.Succeeded);
        var active = nameof(SubscriptionStatus.Active);
        var pastDue = nameof(SubscriptionStatus.PastDue);
        return await _context.Database
            .SqlQuery<long>($"""
                SELECT COALESCE(ROUND(SUM(p."AmountMinor"::numeric / p."PeriodMonths")), 0)::bigint AS "Value"
                FROM "Subscriptions" AS s
                INNER JOIN LATERAL (
                    SELECT x."AmountMinor", x."PeriodMonths"
                    FROM "Payments" AS x
                    WHERE x."SubscriptionId" = s."Id" AND x."Status" = {succeeded} AND x."IsDeleted" = false
                    ORDER BY x."CompletedAt" DESC
                    LIMIT 1
                ) AS p ON true
                WHERE s."IsDeleted" = false AND s."Status" IN ({active}, {pastDue})
                """)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
