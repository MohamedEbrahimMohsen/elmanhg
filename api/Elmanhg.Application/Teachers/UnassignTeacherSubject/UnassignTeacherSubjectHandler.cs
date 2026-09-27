using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Teachers;
using MediatR;

namespace Elmanhg.Application.Teachers.UnassignTeacherSubject;

public sealed class UnassignTeacherSubjectHandler(ITeacherSubjectRepository teacherSubjectRepository, ICurrentUserService currentUserService) : IRequestHandler<UnassignTeacherSubjectCommand>
{
    public async Task Handle(UnassignTeacherSubjectCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var teacherSubject = await teacherSubjectRepository.FirstOrDefaultAsync(x => x.TeacherId == request.TeacherId && x.SubjectId == request.SubjectId, cancellationToken).ConfigureAwait(false);
        if (teacherSubject is null)
        {
            throw new NotFoundCoreException(ErrorCodes.TeacherSubjectNotAssigned);
        }

        teacherSubject.Unassign(currentUserService.UserId.Value);

        await teacherSubjectRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
