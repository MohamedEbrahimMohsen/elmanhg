using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.TrainingExports;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.TrainingExports;

public class TrainingExportRepository(AppDbContext context) : Repository<TrainingExport>(context), ITrainingExportRepository
{
    public async Task<List<Guid>> GetDueIdsAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(x => x.Status == TrainingExportStatus.Pending && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<Guid>> GetExpiredIdsAsync(DateTimeOffset now, int limit, IReadOnlyCollection<Guid> excludedIds, CancellationToken cancellationToken)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(x => ((x.Status == TrainingExportStatus.Completed && x.ExpiresAt <= now) || (x.Status == TrainingExportStatus.Failed && x.FileKey != null)) && !excludedIds.Contains(x.Id))
            .OrderBy(x => x.ExpiresAt)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
