using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Teachers;
using MediatR;
using System.Security.Claims;

namespace Elmanhg.Application.TeacherThreads.CanViewTeacherThreadImage;

public sealed class CanViewTeacherThreadImageHandler(ITeacherThreadRepository teacherThreadRepository, ITeacherSubjectRepository teacherSubjectRepository, ICurrentUserService currentUserService) : IRequestHandler<CanViewTeacherThreadImageQuery, bool>
{
    public async Task<bool> Handle(CanViewTeacherThreadImageQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId || userId == default)
        {
            return false;
        }

        var thread = await teacherThreadRepository.FirstOrDefaultAsync(x => x.Messages.Any(message => message.ImageUrl == request.ImageUrl), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (thread is null)
        {
            return false;
        }

        return thread.StudentId == userId || currentUserService.GetClaim(ClaimTypes.Role) switch
        {
            nameof(UserRole.Admin) => true,
            nameof(UserRole.Teacher) => await teacherSubjectRepository.IsAssignedAsync(userId, thread.SubjectId, cancellationToken).ConfigureAwait(false),
            _ => false,
        };
    }
}
