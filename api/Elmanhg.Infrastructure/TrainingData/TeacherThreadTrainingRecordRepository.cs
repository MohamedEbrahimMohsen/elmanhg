using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.TrainingData;

public class TeacherThreadTrainingRecordRepository(AppDbContext context) : Repository<TeacherThreadTrainingRecord>(context), ITeacherThreadTrainingRecordRepository
{
    public async Task<List<TeacherThreadTrainingRecord>> GetExportPageAsync(TrainingRecordFilter filter, TrainingRecordCursor? after, int limit, CancellationToken cancellationToken)
    {
        var query = _dbSet
            .AsNoTracking()
            .Where(x => x.OccurredAt >= filter.From && x.OccurredAt < filter.To)
            .Where(x => !_dbSet.Any(y => y.ThreadId == x.ThreadId && y.OccurredAt >= filter.From && y.OccurredAt < filter.To && (y.OccurredAt > x.OccurredAt || (y.OccurredAt == x.OccurredAt && y.Trigger == TeacherThreadTrainingTrigger.RatedAfterClose && x.Trigger == TeacherThreadTrainingTrigger.Closed))));
        if (filter.SubjectId is { } subjectId)
        {
            query = query.Where(x => x.SubjectId == subjectId);
        }

        if (after is not null)
        {
            query = query.Where(x => EF.Functions.GreaterThan(ValueTuple.Create(x.OccurredAt, x.Id), ValueTuple.Create(after.OccurredAt, after.Id)));
        }

        return await query
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
