using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Subjects;
using MediatR;

namespace Elmanhg.Application.Subjects.UpdateSubject;

public sealed class UpdateSubjectHandler(ISubjectRepository subjectRepository, ICurrentUserService currentUserService) : IRequestHandler<UpdateSubjectCommand>
{
    public async Task Handle(UpdateSubjectCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var subject = await subjectRepository.GetByIdAsync(request.SubjectId, cancellationToken).ConfigureAwait(false);
        if (subject is null)
        {
            throw new NotFoundCoreException(ErrorCodes.SubjectNotFound);
        }

        subject.Rename(request.Name, currentUserService.UserId.Value);

        await subjectRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
