using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.Storage;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Sessions.Selection;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Elmanhg.Application.Sessions.StartQuizSession;

public sealed class StartQuizSessionHandler(ISessionRepository sessionRepository, ILessonRepository lessonRepository, IQuestionRepository questionRepository, ISubscriptionRepository subscriptionRepository, IOptions<SessionsOptions> sessionsOptions, IOptions<MasteryOptions> masteryOptions, IOptions<SubscriptionsOptions> subscriptionsOptions, Random random, TimeProvider timeProvider, ICurrentUserService currentUserService, ILocalizer localizer, IFileStorage fileStorage, IRuntimeSettings runtimeSettings) : IRequestHandler<StartQuizSessionCommand, SessionResult>
{
    public async Task<SessionResult> Handle(StartQuizSessionCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var lesson = await lessonRepository.GetByIdAsync(request.LessonId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (lesson is null || lesson.State != LessonState.Published)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        var now = timeProvider.GetUtcNow();
        var isTestMode = currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Admin);
        var entitlement = isTestMode ? null : await StudentEntitlementLoader.LoadAsync(subscriptionRepository, runtimeSettings, userId, subscriptionsOptions.Value, now, cancellationToken).ConfigureAwait(false);
        if (entitlement is not null)
        {
            await FreeTierGate.EnsureLessonOpenAsync(entitlement, lesson.Id, lessonRepository, cancellationToken).ConfigureAwait(false);
        }

        var scopeKey = new QuizScope(lesson.Id).ToKey();
        var session = await sessionRepository.FirstOrDefaultAsync(x => x.StudentId == userId && x.Kind == SessionKind.Quiz && x.ScopeKey == scopeKey && x.SubmittedAt == null, cancellationToken, include: query => query.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery()).ConfigureAwait(false);
        if (session is not null)
        {
            session.Resume();
        }
        else
        {
            if (entitlement is not null)
            {
                await FreeTierGate.EnsureQuizQuestionAvailableAsync(entitlement, userId, sessionRepository, subscriptionsOptions.Value, now, cancellationToken).ConfigureAwait(false);
            }

            var questions = await SelectQuestionsAsync(userId, lesson.Id, request.QuestionCount ?? sessionsOptions.Value.DefaultQuizSize, cancellationToken).ConfigureAwait(false);
            session = Session.StartQuiz(userId, lesson, questions, isTestMode);
            await sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        }

        await sessionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var revisions = await questionRepository.GetRevisionsAsync(session.Items.Select(x => x.QuestionId).ToList(), cancellationToken).ConfigureAwait(false);
        return SessionResultGenerator.Generate(session, revisions, localizer, fileStorage);
    }

    private async Task<List<Question>> SelectQuestionsAsync(Guid studentId, Guid lessonId, int count, CancellationToken cancellationToken)
    {
        var candidateIds = await questionRepository.GetServableIdsInLessonAsync(lessonId, cancellationToken).ConfigureAwait(false);
        if (candidateIds.Count == 0)
        {
            return [];
        }

        var summaries = await sessionRepository.GetAttemptSummariesAsync(studentId, candidateIds, masteryOptions.Value.CorrectThreshold, cancellationToken).ConfigureAwait(false);
        var selectedIds = QuestionSelector.Select(candidateIds, summaries, count, random);
        var loaded = await questionRepository.FindAsync(x => selectedIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var questionsById = loaded.ToDictionary(x => x.Id);
        return selectedIds
            .Where(questionsById.ContainsKey)
            .Select(x => questionsById[x])
            .ToList();
    }
}
