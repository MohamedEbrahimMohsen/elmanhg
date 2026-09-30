using Core.DDD.Repositories;

namespace Elmanhg.Domain.TrainingData;

public interface IAttemptTrainingRecordRepository : IRepository<AttemptTrainingRecord>
{
    Task<List<AttemptTrainingRecord>> GetExportPageAsync(TrainingRecordFilter filter, TrainingRecordCursor? after, int limit, CancellationToken cancellationToken);
}
