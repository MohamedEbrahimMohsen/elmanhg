using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Avatar;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Avatar;

public class AvatarMessageUsageRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<AvatarMessageUsage>(context, currentUser, timeProvider), IAvatarMessageUsageRepository
{
    public async Task<int> CountOnDayAsync(Guid studentId, string timeZone, DateOnly day, CancellationToken cancellationToken)
    {
        // Any zone's local midnight is within 14 h of UTC midnight, so the day before is a safe index bound.
        var lowerBound = new DateTimeOffset(day.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return await _context.Database
            .SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value"
                FROM "AvatarMessageUsages" AS u
                WHERE u."StudentId" = {studentId} AND u."CreatedAt" >= {lowerBound} AND u."IsDeleted" = false
                AND (u."CreatedAt" AT TIME ZONE {timeZone})::date = {day}
                """)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
