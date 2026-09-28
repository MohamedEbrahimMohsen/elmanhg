using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.Sessions.SubmitAnswer;

public sealed class SubmitAnswerHandler(ISessionRepository sessionRepository, IQuestionRepository questionRepository, ICurrentUserService currentUserService, ILocalizer localizer) : IRequestHandler<SubmitAnswerCommand, SessionItemResult>
{
    public async Task<SessionItemResult> Handle(SubmitAnswerCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var session = await sessionRepository.FirstOrDefaultAsync(x => x.Id == request.SessionId && x.StudentId == userId, cancellationToken, include: query => query.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery()).ConfigureAwait(false);
        if (session is null)
        {
            throw new NotFoundCoreException(ErrorCodes.SessionNotFound);
        }

        var item = session.GetItem(request.QuestionId);
        if (item is null)
        {
            throw new NotFoundCoreException(ErrorCodes.SessionQuestionNotFound);
        }

        var revisions = await questionRepository.GetRevisionsAsync([item.QuestionId], cancellationToken).ConfigureAwait(false);
        var revision = revisions.FirstOrDefault(x => x.Version == item.QuestionVersion);
        if (revision is null)
        {
            throw new NotFoundCoreException(ErrorCodes.QuestionNotFound);
        }

        var type = revision.ReadSnapshot().Type;
        if (!QuestionAnswerRules.CanRead(type, request.Answer))
        {
            throw new ApplicationValidationCoreException(ErrorCodes.QuestionAnswerInvalid);
        }

        session.RecordAttempt(item, QuestionAnswerRules.Canonicalize(type, request.Answer), revision.Grade(request.Answer), request.TimeTakenMilliseconds);

        await sessionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return SessionResultGenerator.GenerateItem(session, item, revision, localizer);
    }
}
