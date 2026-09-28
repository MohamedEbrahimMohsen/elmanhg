using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Elmanhg.Application.Sessions.StartQuizSession;

public sealed class StartQuizSessionHandler(ISessionRepository sessionRepository, ILessonRepository lessonRepository, IQuestionRepository questionRepository, IOptions<SessionsOptions> sessionsOptions, ICurrentUserService currentUserService, ILocalizer localizer) : IRequestHandler<StartQuizSessionCommand, SessionResult>
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

        var scopeKey = new QuizScope(lesson.Id).ToKey();
        var session = await sessionRepository.FirstOrDefaultAsync(x => x.StudentId == userId && x.Kind == SessionKind.Quiz && x.ScopeKey == scopeKey && x.SubmittedAt == null, cancellationToken, include: query => query.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery()).ConfigureAwait(false);
        if (session is not null)
        {
            session.Resume();
        }
        else
        {
            var questions = await questionRepository.GetRandomServableInLessonAsync(lesson.Id, request.QuestionCount ?? sessionsOptions.Value.DefaultQuizSize, cancellationToken).ConfigureAwait(false);
            session = Session.StartQuiz(userId, lesson, questions, currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Admin));
            await sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        }

        await sessionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var revisions = await questionRepository.GetRevisionsAsync(session.Items.Select(x => x.QuestionId).ToList(), cancellationToken).ConfigureAwait(false);
        return SessionResultGenerator.Generate(session, revisions, localizer);
    }
}
