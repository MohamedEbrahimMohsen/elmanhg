using Core.DDD.Repositories;

namespace Elmanhg.Domain.Teachers;

public interface ITeacherSubjectRepository : IRepository<TeacherSubject>
{
    Task<bool> IsAssignedAsync(Guid teacherId, Guid subjectId, CancellationToken cancellationToken);
}
