using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Subjects.DeleteSubject;

public sealed class DeleteSubjectHandler(ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ICurrentUserService currentUserService) : IRequestHandler<DeleteSubjectCommand>
{
    public async Task Handle(DeleteSubjectCommand request, CancellationToken cancellationToken)
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

        var hasUnits = await unitRepository.AnyInSubjectAsync(subject.Id, cancellationToken).ConfigureAwait(false);
        subject.Delete(hasUnits, currentUserService.UserId.Value);

        await subjectRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
