using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Students.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using MediatR;

namespace Elmanhg.Application.Students.GetSubjectInterests;

public sealed class GetSubjectInterestsHandler(IUserRepository userRepository, ISubjectRepository subjectRepository, ICurrentUserService currentUserService) : IRequestHandler<GetSubjectInterestsQuery, SubjectInterestsResult>
{
    public async Task<SubjectInterestsResult> Handle(GetSubjectInterestsQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var user = await userRepository.GetByIdAsync(currentUserService.UserId.Value, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (user is null)
        {
            throw new NotFoundCoreException(ErrorCodes.UserNotFound);
        }

        var subjects = await subjectRepository.GetAllAsync(cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true).ConfigureAwait(false) ?? [];
        return SubjectInterestsResultGenerator.Generate(user, subjects);
    }
}
