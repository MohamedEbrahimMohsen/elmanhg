using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Analytics;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Analytics;

public class UserActivityDayRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<UserActivityDay>(context, currentUser, timeProvider), IUserActivityDayRepository
{
    public async Task AddIfAbsentAsync(UserActivityDay activity, CancellationToken cancellationToken)
    {
        await _context.Database
            .ExecuteSqlAsync($"""
                INSERT INTO "UserActivityDays" ("Id", "UserId", "Day", "FirstSeenAt", "IsDeleted")
                VALUES ({activity.Id}, {activity.UserId}, {activity.Day}, {activity.FirstSeenAt}, false)
                ON CONFLICT ("UserId", "Day") DO NOTHING
                """, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountActiveStudentsAsync(DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken)
    {
        return await ActiveStudentDays(fromDay, toDay)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<DailyTotal>> CountActiveStudentsByDayAsync(DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken)
    {
        return await ActiveStudentDays(fromDay, toDay)
            .GroupBy(x => x.Day)
            .Select(x => new DailyTotal { Day = x.Key, Value = x.LongCount() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private IQueryable<UserActivityDay> ActiveStudentDays(DateOnly fromDay, DateOnly toDay)
    {
        return _dbSet
            .AsNoTracking()
            .Where(x => x.Day >= fromDay && x.Day <= toDay)
            .Join(_context.Set<User>().Where(x => x.Role == UserRole.Student), activity => activity.UserId, user => user.Id, (activity, user) => activity);
    }
}
