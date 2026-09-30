using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Questions.Shared.Grading;
using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Storage;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Sessions.SubmitAnswer;

public sealed class SubmitAnswerHandler(ISessionRepository sessionRepository, IQuestionRepository questionRepository, IQuestionMasteryRepository questionMasteryRepository, ILessonRepository lessonRepository, ISubscriptionRepository subscriptionRepository, IEssayGradeRepository essayGradeRepository, IMathStepGradeRepository mathStepGradeRepository, IOptions<MasteryOptions> masteryOptions, IOptions<SubscriptionsOptions> subscriptionsOptions, IOptions<ContentOptions> contentOptions, IOptions<SessionsOptions> sessionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService, ILocalizer localizer, IAiMathCheckClient mathCheckClient, IMathCheckRateLimiter mathCheckRateLimiter, IFileStorage fileStorage) : IRequestHandler<SubmitAnswerCommand, SessionItemResult>
{
    public async Task<SessionItemResult> Handle(SubmitAnswerCommand request, CancellationToken cancellationToken)
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

        var item = session.GetItem(request.QuestionId);
        if (item is null)
        {
            throw new NotFoundCoreException(ErrorCodes.SessionQuestionNotFound);
        }

        if (!session.IsTestMode && session.FindAttempt(item.QuestionId) is null && item.SavedAnswer is null)
        {
            await EnsureFreeTierAsync(userId, session, cancellationToken).ConfigureAwait(false);
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

        if (QuestionAnswerRules.TryReadWrittenEssay(type, request.Answer, out var essayText))
        {
            await QuizEssaySubmission.SubmitAsync(session, item, essayText, request.TimeTakenMilliseconds, questionRepository, essayGradeRepository, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await AnswerAsync(session, item, revision, type, request, userId, cancellationToken).ConfigureAwait(false);
        }

        await sessionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return SessionResultGenerator.GenerateItem(session, item, revision, localizer, fileStorage);
    }

    private async Task EnsureFreeTierAsync(Guid studentId, Session session, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var options = subscriptionsOptions.Value;
        var entitlement = await StudentEntitlementLoader.LoadAsync(subscriptionRepository, studentId, options, now, cancellationToken).ConfigureAwait(false);
        await FreeTierGate.EnsureLessonOpenAsync(entitlement, QuizScope.FromJson(session.Scope).LessonId, lessonRepository, cancellationToken).ConfigureAwait(false);
        await FreeTierGate.EnsureQuizQuestionAvailableAsync(entitlement, studentId, sessionRepository, options, now, cancellationToken).ConfigureAwait(false);
    }

    private async Task AnswerAsync(Session session, SessionItem item, QuestionRevision revision, QuestionType type, SubmitAnswerCommand request, Guid userId, CancellationToken cancellationToken)
    {
        var answer = QuestionAnswerRules.Canonicalize(type, request.Answer);
        if (session.IsReplay(item, answer))
        {
            return;
        }

        if (type == QuestionType.MathSteps && MathStepsAnswerRules.HasFinalAnswer(request.Answer) && !mathCheckRateLimiter.TryAcquire(userId))
        {
            throw new RateLimitExceededCoreException(ErrorCodes.TooManyRequests);
        }

        var decision = await AnswerGrader.DecideAsync(revision, request.Answer, mathCheckClient, cancellationToken).ConfigureAwait(false);
        if (decision.Grade is not null)
        {
            await QuizAttemptRecorder.RecordAsync(session, item, decision.Grade, answer, request.TimeTakenMilliseconds, questionMasteryRepository, masteryOptions.Value.CorrectThreshold, cancellationToken).ConfigureAwait(false);
            return;
        }

        await QuizMathStepsSubmission.SubmitAsync(session, item, answer, decision.Verdict, request.TimeTakenMilliseconds, questionRepository, mathStepGradeRepository, cancellationToken).ConfigureAwait(false);
    }
}
