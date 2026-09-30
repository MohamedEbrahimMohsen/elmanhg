using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Progress.Shared;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Progress.GetSubjectProgress;

public sealed class GetSubjectProgressHandler(IQuestionMasteryRepository questionMasteryRepository, ISessionRepository sessionRepository, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ICurrentUserService currentUserService) : IRequestHandler<GetSubjectProgressQuery, List<SubjectProgressResult>>
{
    public async Task<List<SubjectProgressResult>> Handle(GetSubjectProgressQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        return await SubjectProgressLoader.LoadAsync(questionMasteryRepository, sessionRepository, subjectRepository, unitRepository, currentUserService.UserId.Value, cancellationToken).ConfigureAwait(false);
    }
}
