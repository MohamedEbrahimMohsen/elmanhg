using Core.DDD.Repositories;

namespace Elmanhg.Domain.TrainingData;

public interface ITeacherThreadTrainingRecordRepository : IRepository<TeacherThreadTrainingRecord>
{
    Task<List<TeacherThreadTrainingRecord>> GetExportPageAsync(TrainingRecordFilter filter, TrainingRecordCursor? after, int limit, CancellationToken cancellationToken);
}
