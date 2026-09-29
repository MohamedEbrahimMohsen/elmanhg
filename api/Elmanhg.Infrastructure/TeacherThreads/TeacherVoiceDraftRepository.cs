using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.TeacherThreads;

public class TeacherVoiceDraftRepository(AppDbContext context) : Repository<TeacherVoiceDraft>(context), ITeacherVoiceDraftRepository
{
    public async Task<List<Guid>> GetDueIdsAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(x => x.Status == TeacherVoiceDraftStatus.Pending && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
