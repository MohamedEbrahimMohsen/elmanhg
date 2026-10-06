using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Core.Storage;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Exams.GetExamSession;

public sealed class GetExamSessionHandler(ISessionRepository sessionRepository, IQuestionRepository questionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IOptions<ExamsOptions> examsOptions, IOptions<MasteryOptions> masteryOptions, TimeProvider timeProvider, ICurrentUserService currentUserService, ILocalizer localizer, IFileStorage fileStorage) : IRequestHandler<GetExamSessionQuery, ExamSessionResult>
{
    public async Task<ExamSessionResult> Handle(GetExamSessionQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var session = await sessionRepository.FirstOrDefaultAsync(x => x.Id == request.SessionId && x.StudentId == userId && x.Kind != SessionKind.Quiz, cancellationToken, include: query => query.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery(), asNoTracking: true).ConfigureAwait(false);
        if (session is null)
        {
            throw new NotFoundCoreException(ErrorCodes.SessionNotFound);
        }

        var revisions = await questionRepository.GetRevisionsAsync(session.Items.Select(x => x.QuestionId).ToList(), cancellationToken).ConfigureAwait(false);
        return await ExamSessionResultLoader.LoadAsync(session, revisions, questionRepository, lessonRepository, unitRepository, subjectRepository, masteryOptions.Value.CorrectThreshold, examsOptions.Value.WeakestObjectiveCount, timeProvider.GetUtcNow(), localizer, fileStorage, cancellationToken).ConfigureAwait(false);
    }
}
