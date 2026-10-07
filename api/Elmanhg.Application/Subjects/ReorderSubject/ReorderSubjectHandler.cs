using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Subjects;
using MediatR;

namespace Elmanhg.Application.Subjects.ReorderSubject;

public sealed class ReorderSubjectHandler(ISubjectRepository subjectRepository, ICurrentUserService currentUserService) : IRequestHandler<ReorderSubjectCommand>
{
    public async Task Handle(ReorderSubjectCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var siblings = await subjectRepository.FindAsync(_ => true, cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate)).ConfigureAwait(false);
        var subject = siblings.FirstOrDefault(x => x.Id == request.SubjectId);
        if (subject is null)
        {
            throw new NotFoundCoreException(ErrorCodes.SubjectNotFound);
        }

        siblings.Remove(subject);
        siblings.Insert(Math.Min(request.Position, siblings.Count + 1) - 1, subject);
        for (var index = 0; index < siblings.Count; index++)
        {
            siblings[index].MoveTo(index + 1, userId);
        }

        await subjectRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
