using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.TeacherThreads;

public class TeacherThreadSlaEventRepository(AppDbContext context) : Repository<TeacherThreadSlaEvent>(context), ITeacherThreadSlaEventRepository
{
    public async Task<int> CountBreachesAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, CancellationToken cancellationToken)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(x => x.Kind == TeacherThreadSlaEventKind.Breach && x.OccurredAt >= start && x.OccurredAt < end)
            .Join(_context.Set<TeacherThread>().Where(x => subjectId == null || x.SubjectId == subjectId), slaEvent => slaEvent.ThreadId, thread => thread.Id, (slaEvent, thread) => slaEvent.Id)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
