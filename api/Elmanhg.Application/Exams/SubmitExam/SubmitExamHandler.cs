using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Core.Storage;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Exams.SubmitExam;

public sealed class SubmitExamHandler(ISessionRepository sessionRepository, IQuestionRepository questionRepository, IQuestionMasteryRepository questionMasteryRepository, IEssayGradeRepository essayGradeRepository, IMathStepGradeRepository mathStepGradeRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IOptions<ExamsOptions> examsOptions, IOptions<MasteryOptions> masteryOptions, TimeProvider timeProvider, ICurrentUserService currentUserService, ILocalizer localizer, IAiMathCheckClient mathCheckClient, IFileStorage fileStorage) : IRequestHandler<SubmitExamCommand, ExamSessionResult>
{
    public async Task<ExamSessionResult> Handle(SubmitExamCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var now = timeProvider.GetUtcNow();
        var threshold = masteryOptions.Value.CorrectThreshold;
        var session = await sessionRepository.GetRequiredAsync(x => x.Id == request.SessionId && x.StudentId == userId && x.Kind != SessionKind.Quiz, ErrorCodes.SessionNotFound, cancellationToken, include: query => query.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery()).ConfigureAwait(false);

        var revisions = await questionRepository.GetRevisionsAsync(session.Items.Select(x => x.QuestionId).ToList(), cancellationToken).ConfigureAwait(false);
        await ExamSubmission.SubmitAsync(session, revisions, questionRepository, questionMasteryRepository, essayGradeRepository, mathStepGradeRepository, mathCheckClient, threshold, now, cancellationToken).ConfigureAwait(false);

        await sessionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await ExamSessionResultLoader.LoadAsync(session, revisions, questionRepository, lessonRepository, unitRepository, subjectRepository, threshold, examsOptions.Value.WeakestObjectiveCount, now, localizer, fileStorage, cancellationToken).ConfigureAwait(false);
    }
}
