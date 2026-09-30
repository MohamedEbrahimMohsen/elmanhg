using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Units;
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

    public async Task<Dictionary<Guid, int>> CountByUnitAsync(IReadOnlyCollection<Guid> unitIds, bool publishedOnly, CancellationToken cancellationToken)
    {
        return await _dbSet
            .Where(x => unitIds.Contains(x.UnitId) && (!publishedOnly || x.State == LessonState.Published))
            .GroupBy(x => x.UnitId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<LessonPosition>> GetPublishedPositionsAsync(CancellationToken cancellationToken)
    {
        return await _dbSet
            .Where(x => x.State == LessonState.Published)
            .Select(x => new LessonPosition(x.Id, x.UnitId, x.Order, x.CreationDate))
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<LessonPosition>> GetPublishedSiblingPositionsAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        return await _dbSet
            .Where(x => x.State == LessonState.Published && _dbSet.Any(lesson => lesson.Id == lessonId && lesson.UnitId == x.UnitId))
            .Select(x => new LessonPosition(x.Id, x.UnitId, x.Order, x.CreationDate))
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<LessonStateCount>> CountByStateAsync(Guid? subjectId, CancellationToken cancellationToken)
    {
        return await _dbSet
            .AsNoTracking()
            .Join(_context.Set<CurriculumUnit>(), lesson => lesson.UnitId, unit => unit.Id, (lesson, unit) => new { lesson.State, unit.SubjectId })
            .Where(x => subjectId == null || x.SubjectId == subjectId)
            .GroupBy(x => x.State)
            .Select(x => new LessonStateCount(x.Key, x.Count()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
