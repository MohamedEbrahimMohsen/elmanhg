using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.TeacherThreads;

public class TeacherThreadOutOfAppReminderRepository(AppDbContext context) : Repository<TeacherThreadOutOfAppReminder>(context), ITeacherThreadOutOfAppReminderRepository
{
    public async Task<bool> IsRecordedAsync(Guid threadId, CancellationToken cancellationToken)
    {
        return await _dbSet
            .AsNoTracking()
            .AnyAsync(x => x.ThreadId == threadId, cancellationToken)
            .ConfigureAwait(false);
    }
}
