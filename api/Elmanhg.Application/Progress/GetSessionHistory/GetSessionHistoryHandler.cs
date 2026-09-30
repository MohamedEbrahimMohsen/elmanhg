using Core.DDD.Models;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Progress.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Progress.GetSessionHistory;

public sealed class GetSessionHistoryHandler(ISessionRepository sessionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ICurrentUserService currentUserService) : IRequestHandler<GetSessionHistoryQuery, PageData<SessionHistoryItemResult>>
{
    public async Task<PageData<SessionHistoryItemResult>> Handle(GetSessionHistoryQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        return await SessionHistoryLoader.LoadAsync(sessionRepository, lessonRepository, unitRepository, currentUserService.UserId.Value, request.Kind, request.PageNumber, request.PageSize, cancellationToken).ConfigureAwait(false);
    }
}
