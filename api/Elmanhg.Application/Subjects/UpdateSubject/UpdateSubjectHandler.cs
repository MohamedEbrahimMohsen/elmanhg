using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Subjects;
using MediatR;

namespace Elmanhg.Application.Subjects.UpdateSubject;

public sealed class UpdateSubjectHandler(ISubjectRepository subjectRepository, ICurrentUserService currentUserService) : IRequestHandler<UpdateSubjectCommand>
{
    public async Task Handle(UpdateSubjectCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var subject = await subjectRepository.GetRequiredAsync(request.SubjectId, ErrorCodes.SubjectNotFound, cancellationToken).ConfigureAwait(false);

        subject.Rename(request.Name, userId);

        await subjectRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
