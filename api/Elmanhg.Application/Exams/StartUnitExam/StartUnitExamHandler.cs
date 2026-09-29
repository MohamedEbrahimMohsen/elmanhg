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
using Elmanhg.Domain.Sessions.Exams;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Elmanhg.Application.Exams.StartUnitExam;

public sealed class StartUnitExamHandler(ISessionRepository sessionRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, ILessonRepository lessonRepository, ILessonOpeningRepository lessonOpeningRepository, IQuestionRepository questionRepository, IExamBlueprintRepository examBlueprintRepository, IQuestionMasteryRepository questionMasteryRepository, IOptions<ExamsOptions> examsOptions, IOptions<MasteryOptions> masteryOptions, Random random, TimeProvider timeProvider, ICurrentUserService currentUserService, ILocalizer localizer) : IRequestHandler<StartUnitExamCommand, ExamSessionResult>
{
    public async Task<ExamSessionResult> Handle(StartUnitExamCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var now = timeProvider.GetUtcNow();
        var threshold = masteryOptions.Value.CorrectThreshold;
        var unit = await unitRepository.GetByIdAsync(request.UnitId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (unit is null)
        {
            throw new NotFoundCoreException(ErrorCodes.UnitNotFound);
        }

        var open = await sessionRepository.FirstOrDefaultAsync(x => x.StudentId == userId && x.Kind != SessionKind.Quiz && x.SubmittedAt == null, cancellationToken, include: query => query.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery()).ConfigureAwait(false);
        if (open is not null && open.ScopeKey != new UnitExamScope(unit.Id).ToKey())
        {
            throw new ConflictCoreException(ErrorCodes.ExamAlreadyInProgress);
        }

        var session = open ?? await StartAsync(userId, unit, now, cancellationToken).ConfigureAwait(false);
        var revisions = await questionRepository.GetRevisionsAsync(session.Items.Select(x => x.QuestionId).ToList(), cancellationToken).ConfigureAwait(false);
        if (open is null)
        {
            await sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        }
        else if (open.IsPastDeadline(now, examsOptions.Value.DeadlineGrace))
        {
            await ExamSubmission.SubmitAsync(open, revisions, questionMasteryRepository, threshold, now, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            open.Resume();
        }

        await sessionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await ExamSessionResultLoader.LoadAsync(session, revisions, questionRepository, lessonRepository, unitRepository, subjectRepository, threshold, examsOptions.Value.WeakestObjectiveCount, now, localizer, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Session> StartAsync(Guid userId, CurriculumUnit unit, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var isTestMode = currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Admin);
        if (examsOptions.Value.RequireAllLessonsOpened && !isTestMode)
        {
            await ExamLessonGate.EnsureOpenedAsync(userId, [unit.Id], lessonRepository, lessonOpeningRepository, cancellationToken).ConfigureAwait(false);
        }

        var blueprints = await examBlueprintRepository.FindAsync(x => x.SubjectId == unit.SubjectId && (x.UnitId == unit.Id || x.UnitId == null), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var blueprint = ExamBlueprintResolution.ForUnit(blueprints, unit);
        if (blueprint is null)
        {
            throw new BadRequestCoreException(ErrorCodes.UnitExamNoBlueprint);
        }

        var questions = await DrawAsync(userId, unit, blueprint, cancellationToken).ConfigureAwait(false);
        var lessonIds = questions
            .Select(x => x.LessonId)
            .Distinct()
            .ToList();
        var lessons = await lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return Session.StartUnitExam(userId, unit, blueprint, questions, lessons, isTestMode, now);
    }

    private async Task<List<Question>> DrawAsync(Guid userId, CurriculumUnit unit, ExamBlueprint blueprint, CancellationToken cancellationToken)
    {
        var candidates = await questionRepository.GetServableExamCandidatesAsync([unit.Id], cancellationToken).ConfigureAwait(false);
        blueprint.EnsureServable(candidates.GroupBy(x => x.Type).ToDictionary(x => x.Key, x => x.Count()));
        var candidateIds = candidates
            .Select(x => x.QuestionId)
            .ToList();
        var masteries = await questionMasteryRepository.FindAsync(x => x.StudentId == userId && x.IsMastered && candidateIds.Contains(x.QuestionId), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var mastered = masteries
            .Select(x => x.QuestionId)
            .ToHashSet();
        var selectedIds = ExamQuestionSelector.Select(candidates, mastered, blueprint.GetTypeCounts(), blueprint.GetDifficultyMix(), random);
        var loaded = await questionRepository.FindAsync(x => selectedIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var questionsById = loaded.ToDictionary(x => x.Id);
        return selectedIds
            .Where(questionsById.ContainsKey)
            .Select(x => questionsById[x])
            .ToList();
    }
}
