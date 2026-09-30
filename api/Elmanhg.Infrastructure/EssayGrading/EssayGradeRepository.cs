using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.EssayGrading;

public class EssayGradeRepository(AppDbContext context) : Repository<EssayGrade>(context), IEssayGradeRepository
{
    public async Task<List<Guid>> GetDueIdsAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(x => (x.Status == EssayGradeStatus.Pending && x.NextAttemptAt <= now) || (x.Status == EssayGradeStatus.Graded && x.AppliedAt == null))
            .OrderBy(x => x.Status == EssayGradeStatus.Pending)
            .ThenBy(x => x.NextAttemptAt)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
