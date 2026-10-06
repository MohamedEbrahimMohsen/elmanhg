using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Teachers;
using MediatR;

namespace Elmanhg.Application.Teachers.UnassignTeacherSubject;

public sealed class UnassignTeacherSubjectHandler(ITeacherSubjectRepository teacherSubjectRepository, ICurrentUserService currentUserService) : IRequestHandler<UnassignTeacherSubjectCommand>
{
    public async Task Handle(UnassignTeacherSubjectCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var teacherSubject = await teacherSubjectRepository.GetRequiredAsync(x => x.TeacherId == request.TeacherId && x.SubjectId == request.SubjectId, ErrorCodes.TeacherSubjectNotAssigned, cancellationToken).ConfigureAwait(false);

        teacherSubject.Unassign(userId);

        await teacherSubjectRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
