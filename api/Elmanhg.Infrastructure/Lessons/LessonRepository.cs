using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Lessons;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Lessons;

public class LessonRepository(AppDbContext context) : Repository<Lesson>(context), ILessonRepository
{
    public async Task<Lesson?> GetWithObjectivesAsync(Guid lessonId, bool asNoTracking, CancellationToken cancellationToken)
    {
        var query = _dbSet.Include(x => x.Objectives.OrderBy(o => o.Order)).AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(x => x.Id == lessonId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> AnyInUnitAsync(Guid unitId, CancellationToken cancellationToken)
    {
        return await _dbSet.AnyAsync(x => x.UnitId == unitId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Dictionary<Guid, int>> CountByUnitAsync(IReadOnlyCollection<Guid> unitIds, CancellationToken cancellationToken)
    {
        return await _dbSet
            .Where(x => unitIds.Contains(x.UnitId))
            .GroupBy(x => x.UnitId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken)
            .ConfigureAwait(false);
    }
}
