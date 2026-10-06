using Core.DDD.Repositories;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Exams.SaveExamAnswer;

public sealed class SaveExamAnswerHandler(ISessionRepository sessionRepository, IQuestionRepository questionRepository, IOptions<ExamsOptions> examsOptions, IOptions<ContentOptions> contentOptions, IOptions<SessionsOptions> sessionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<SaveExamAnswerCommand, ExamAnswerSavedResult>
{
    public async Task<ExamAnswerSavedResult> Handle(SaveExamAnswerCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var now = timeProvider.GetUtcNow();
        var session = await sessionRepository.GetRequiredAsync(x => x.Id == request.SessionId && x.StudentId == userId && x.Kind != SessionKind.Quiz, ErrorCodes.SessionNotFound, cancellationToken, include: query => query.Include(x => x.Items)).ConfigureAwait(false);

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

        if (QuestionAnswerRules.IsRawAnswerTooLong(type, request.Answer, sessionsOptions.Value) || QuestionAnswerRules.ExceedsLimits(type, request.Answer, sessionsOptions.Value))
        {
            throw new ApplicationValidationCoreException(ErrorCodes.AttemptAnswerTooLong);
        }

        if (QuestionAnswerRules.IsEssayTooLong(type, request.Answer, contentOptions.Value.QuestionEssayAnswerMaxLength))
        {
            throw new ApplicationValidationCoreException(ErrorCodes.QuestionEssayAnswerTooLong);
        }

        session.SaveExamAnswer(item, QuestionAnswerRules.Canonicalize(type, request.Answer), examsOptions.Value.DeadlineGrace, now);

        await sessionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ExamAnswerSavedResult(item.QuestionId, item.AnswerSavedAt.GetValueOrDefault());
    }
}
