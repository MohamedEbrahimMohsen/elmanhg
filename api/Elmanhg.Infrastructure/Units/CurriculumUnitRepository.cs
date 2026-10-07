using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Units;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Units;

public class CurriculumUnitRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<CurriculumUnit>(context, currentUser, timeProvider), ICurriculumUnitRepository
{
    public async Task<bool> AnyInSubjectAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        return await _dbSet.AnyAsync(x => x.SubjectId == subjectId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Dictionary<Guid, int>> CountBySubjectAsync(IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken)
    {
        return await _dbSet
            .Where(x => subjectIds.Contains(x.SubjectId))
            .GroupBy(x => x.SubjectId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken)
            .ConfigureAwait(false);
    }
}
