using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Lessons;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Lessons;

public class LessonOpeningRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<LessonOpening>(context, currentUser, timeProvider), ILessonOpeningRepository
{
    public async Task<bool> IsOpenedAsync(Guid studentId, Guid lessonId, CancellationToken cancellationToken)
    {
        return await _dbSet.AnyAsync(x => x.StudentId == studentId && x.LessonId == lessonId, cancellationToken).ConfigureAwait(false);
    }
}
