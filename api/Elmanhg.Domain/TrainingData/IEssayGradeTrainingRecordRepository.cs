using Core.DDD.Repositories;

namespace Elmanhg.Domain.TrainingData;

public interface IEssayGradeTrainingRecordRepository : IRepository<EssayGradeTrainingRecord>
{
    Task<List<EssayGradeTrainingRecord>> GetExportPageAsync(TrainingRecordFilter filter, TrainingRecordCursor? after, int limit, CancellationToken cancellationToken);
}
