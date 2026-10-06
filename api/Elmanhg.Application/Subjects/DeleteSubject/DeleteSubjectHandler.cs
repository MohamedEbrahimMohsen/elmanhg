using Core.DDD.Repositories;
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
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var subject = await subjectRepository.GetRequiredAsync(request.SubjectId, ErrorCodes.SubjectNotFound, cancellationToken).ConfigureAwait(false);

        var hasUnits = await unitRepository.AnyInSubjectAsync(subject.Id, cancellationToken).ConfigureAwait(false);
        subject.Delete(hasUnits, userId);

        await subjectRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
