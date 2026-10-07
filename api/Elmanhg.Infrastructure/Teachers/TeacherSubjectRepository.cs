using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Teachers;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Teachers;

public class TeacherSubjectRepository(AppDbContext context) : Repository<TeacherSubject>(context), ITeacherSubjectRepository
{
    public async Task<bool> IsAssignedAsync(Guid teacherId, Guid subjectId, CancellationToken cancellationToken)
    {
        return await _dbSet.AnyAsync(x => x.TeacherId == teacherId && x.SubjectId == subjectId, cancellationToken).ConfigureAwait(false);
    }
}
