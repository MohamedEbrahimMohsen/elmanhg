using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.Sessions.FinishSession;

public sealed class FinishSessionHandler(ISessionRepository sessionRepository, IQuestionRepository questionRepository, ICurrentUserService currentUserService, ILocalizer localizer) : IRequestHandler<FinishSessionCommand, SessionResult>
{
    public async Task<SessionResult> Handle(FinishSessionCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var session = await sessionRepository.FirstOrDefaultAsync(x => x.Id == request.SessionId && x.StudentId == userId && x.Kind == SessionKind.Quiz, cancellationToken, include: query => query.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery()).ConfigureAwait(false);
        if (session is null)
        {
            throw new NotFoundCoreException(ErrorCodes.SessionNotFound);
        }

        session.Submit();

        await sessionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var revisions = await questionRepository.GetRevisionsAsync(session.Items.Select(x => x.QuestionId).ToList(), cancellationToken).ConfigureAwait(false);
        return SessionResultGenerator.Generate(session, revisions, localizer);
    }
}
