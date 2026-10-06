using Core.DDD.Repositories;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using MediatR;

namespace Elmanhg.Application.Students.SaveSubjectInterests;

public sealed class SaveSubjectInterestsHandler(IUserRepository userRepository, ISubjectRepository subjectRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<SaveSubjectInterestsCommand>
{
    public async Task Handle(SaveSubjectInterestsCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var subjectIds = request.SubjectIds.ToList();
        if (subjectIds.Count > 0 && await subjectRepository.CountAsync(cancellationToken, x => subjectIds.Contains(x.Id)).ConfigureAwait(false) != subjectIds.Count)
        {
            throw new NotFoundCoreException(ErrorCodes.SubjectNotFound);
        }

        var user = await userRepository.GetRequiredAsync(userId, ErrorCodes.UserNotFound, cancellationToken).ConfigureAwait(false);

        user.ChooseSubjectInterests(subjectIds, timeProvider.GetUtcNow());

        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
