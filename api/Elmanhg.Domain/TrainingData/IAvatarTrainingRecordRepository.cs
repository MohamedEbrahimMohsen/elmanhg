using Core.DDD.Repositories;

namespace Elmanhg.Domain.TrainingData;

public interface IAvatarTrainingRecordRepository : IRepository<AvatarTrainingRecord>
{
    Task<List<AvatarTrainingRecord>> GetExportPageAsync(TrainingRecordFilter filter, TrainingRecordCursor? after, int limit, CancellationToken cancellationToken);
}
