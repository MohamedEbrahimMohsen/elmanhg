using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Subscriptions;

public class PaymentRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<Payment>(context, currentUser, timeProvider), IPaymentRepository
{
    public async Task<PaymentTotals> GetTotalsAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var succeeded = nameof(PaymentStatus.Succeeded);
        var refunded = nameof(PaymentStatus.Refunded);
        var failed = nameof(PaymentStatus.Failed);
        return await _context.Database
            .SqlQuery<PaymentTotals>($"""
                SELECT COUNT(*) FILTER (WHERE p."Status" IN ({succeeded}, {refunded}) AND p."CompletedAt" >= {start} AND p."CompletedAt" < {end})::int AS "Succeeded",
                COUNT(*) FILTER (WHERE p."Status" = {failed} AND p."CompletedAt" >= {start} AND p."CompletedAt" < {end})::int AS "Failed",
                COALESCE(SUM(p."AmountMinor") FILTER (WHERE p."Status" IN ({succeeded}, {refunded}) AND p."CompletedAt" >= {start} AND p."CompletedAt" < {end}), 0)::bigint AS "GrossMinor",
                COUNT(*) FILTER (WHERE p."RefundedAt" >= {start} AND p."RefundedAt" < {end})::int AS "Refunds",
                COALESCE(SUM(p."AmountMinor") FILTER (WHERE p."RefundedAt" >= {start} AND p."RefundedAt" < {end}), 0)::bigint AS "RefundedMinor"
                FROM "Payments" AS p
                WHERE p."IsDeleted" = false AND ((p."CompletedAt" >= {start} AND p."CompletedAt" < {end}) OR (p."RefundedAt" >= {start} AND p."RefundedAt" < {end}))
                """)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<DailyTotal>> GetRevenueByDayAsync(MetricsWindow window, CancellationToken cancellationToken)
    {
        var succeeded = nameof(PaymentStatus.Succeeded);
        var refunded = nameof(PaymentStatus.Refunded);
        return await _context.Database
            .SqlQuery<DailyTotal>($"""
                SELECT (p."CompletedAt" AT TIME ZONE {window.TimeZone})::date AS "Day", SUM(p."AmountMinor")::bigint AS "Value"
                FROM "Payments" AS p
                WHERE p."IsDeleted" = false AND p."Status" IN ({succeeded}, {refunded}) AND p."CompletedAt" >= {window.Start} AND p."CompletedAt" < {window.End}
                GROUP BY 1
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
