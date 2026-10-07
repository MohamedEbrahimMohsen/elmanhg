using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.TeacherThreads;

public class TeacherThreadOutOfAppReminderRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<TeacherThreadOutOfAppReminder>(context, currentUser, timeProvider), ITeacherThreadOutOfAppReminderRepository
{
    public async Task<bool> IsRecordedAsync(Guid threadId, CancellationToken cancellationToken)
    {
        return await _dbSet
            .AsNoTracking()
            .AnyAsync(x => x.ThreadId == threadId, cancellationToken)
            .ConfigureAwait(false);
    }
}
