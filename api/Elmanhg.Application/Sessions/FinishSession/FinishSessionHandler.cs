using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Core.Storage;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.Sessions.FinishSession;

public sealed class FinishSessionHandler(ISessionRepository sessionRepository, IQuestionRepository questionRepository, ICurrentUserService currentUserService, ILocalizer localizer, IFileStorage fileStorage) : IRequestHandler<FinishSessionCommand, SessionResult>
{
    public async Task<SessionResult> Handle(FinishSessionCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var session = await sessionRepository.GetRequiredAsync(x => x.Id == request.SessionId && x.StudentId == userId && x.Kind == SessionKind.Quiz, ErrorCodes.SessionNotFound, cancellationToken, include: query => query.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery()).ConfigureAwait(false);

        session.Submit();

        await sessionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var revisions = await questionRepository.GetRevisionsAsync(session.Items.Select(x => x.QuestionId).ToList(), cancellationToken).ConfigureAwait(false);
        return SessionResultGenerator.Generate(session, revisions, localizer, fileStorage);
    }
}
