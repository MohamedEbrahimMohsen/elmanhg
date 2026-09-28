using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.Sessions.GetSession;

public sealed class GetSessionHandler(ISessionRepository sessionRepository, IQuestionRepository questionRepository, ICurrentUserService currentUserService, ILocalizer localizer) : IRequestHandler<GetSessionQuery, SessionResult>
{
    public async Task<SessionResult> Handle(GetSessionQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var session = await sessionRepository.FirstOrDefaultAsync(x => x.Id == request.SessionId && x.StudentId == userId, cancellationToken, include: query => query.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery(), asNoTracking: true).ConfigureAwait(false);
        if (session is null)
        {
            throw new NotFoundCoreException(ErrorCodes.SessionNotFound);
        }

        var revisions = await questionRepository.GetRevisionsAsync(session.Items.Select(x => x.QuestionId).ToList(), cancellationToken).ConfigureAwait(false);
        return SessionResultGenerator.Generate(session, revisions, localizer);
    }
}
