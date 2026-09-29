using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Teachers;

namespace Elmanhg.Application.TeacherInbox.Shared;

public static class TeacherInboxAccess
{
    public static async Task EnsureCanAccessAsync(Guid subjectId, Guid userId, string? role, ITeacherSubjectRepository teacherSubjectRepository, CancellationToken cancellationToken)
    {
        if (role == nameof(UserRole.Admin))
        {
            return;
        }

        if (!await teacherSubjectRepository.IsAssignedAsync(userId, subjectId, cancellationToken).ConfigureAwait(false))
        {
            throw new ForbiddenCoreException(ErrorCodes.SubjectOutOfScope);
        }
    }
}
