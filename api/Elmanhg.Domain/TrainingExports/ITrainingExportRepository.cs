using Core.DDD.Repositories;

namespace Elmanhg.Domain.TrainingExports;

public interface ITrainingExportRepository : IRepository<TrainingExport>
{
    Task<List<Guid>> GetDueIdsAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    Task<List<Guid>> GetExpiredIdsAsync(DateTimeOffset now, int limit, IReadOnlyCollection<Guid> excludedIds, CancellationToken cancellationToken);
}
