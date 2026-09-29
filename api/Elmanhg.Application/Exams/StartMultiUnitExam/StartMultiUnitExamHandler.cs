using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Elmanhg.Application.Exams.StartMultiUnitExam;

public sealed class StartMultiUnitExamHandler(ISessionRepository sessionRepository, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, ILessonOpeningRepository lessonOpeningRepository, IQuestionRepository questionRepository, IExamBlueprintRepository examBlueprintRepository, IQuestionMasteryRepository questionMasteryRepository, IOptions<ExamsOptions> examsOptions, IOptions<ExamBlueprintsOptions> examBlueprintsOptions, IOptions<MasteryOptions> masteryOptions, Random random, TimeProvider timeProvider, ICurrentUserService currentUserService, ILocalizer localizer) : IRequestHandler<StartMultiUnitExamCommand, ExamSessionResult>
{
    public async Task<ExamSessionResult> Handle(StartMultiUnitExamCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var now = timeProvider.GetUtcNow();
        var threshold = masteryOptions.Value.CorrectThreshold;
        var input = request.Selection;
        var selection = await MultiUnitExamPlanner.LoadUnitsAsync(input.SubjectId, input.UnitIds ?? [], subjectRepository, unitRepository, cancellationToken).ConfigureAwait(false);
        var key = new MultiUnitExamScope(input.SubjectId, selection.Units.Select(x => x.Id).ToList(), input.Size).ToKey();
        var open = await sessionRepository.FirstOrDefaultAsync(x => x.StudentId == userId && x.Kind != SessionKind.Quiz && x.SubmittedAt == null, cancellationToken, include: query => query.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery()).ConfigureAwait(false);
        if (open is not null && open.ScopeKey != key)
        {
            throw new ConflictCoreException(ErrorCodes.ExamAlreadyInProgress);
        }

        var session = open;
        if (session is null)
        {
            var isTestMode = currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Admin);
            if (examsOptions.Value.RequireAllLessonsOpened && !isTestMode)
            {
                await ExamLessonGate.EnsureOpenedAsync(userId, selection.Units.Select(x => x.Id).ToList(), lessonRepository, lessonOpeningRepository, cancellationToken).ConfigureAwait(false);
            }

            var plan = await MultiUnitExamPlanner.PlanAsync(selection, input.Size, examBlueprintRepository, examBlueprintsOptions.Value.MaxTimeLimitMinutes, cancellationToken).ConfigureAwait(false);
            session = await MultiUnitExamDraw.StartAsync(userId, selection, plan, input.Size, isTestMode, now, lessonRepository, questionRepository, questionMasteryRepository, random, cancellationToken).ConfigureAwait(false);
            await sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        }

        var revisions = await questionRepository.GetRevisionsAsync(session.Items.Select(x => x.QuestionId).ToList(), cancellationToken).ConfigureAwait(false);
        if (open is not null && open.IsPastDeadline(now, examsOptions.Value.DeadlineGrace))
        {
            await ExamSubmission.SubmitAsync(open, revisions, questionMasteryRepository, threshold, now, cancellationToken).ConfigureAwait(false);
        }
        else if (open is not null)
        {
            open.Resume();
        }

        await sessionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await ExamSessionResultLoader.LoadAsync(session, revisions, questionRepository, lessonRepository, unitRepository, subjectRepository, threshold, examsOptions.Value.WeakestObjectiveCount, now, localizer, cancellationToken).ConfigureAwait(false);
    }
}
