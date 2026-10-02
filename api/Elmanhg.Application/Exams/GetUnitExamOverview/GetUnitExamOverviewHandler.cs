using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.ExamBlueprints.Shared;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;
using System.Security.Claims;

namespace Elmanhg.Application.Exams.GetUnitExamOverview;

public sealed class GetUnitExamOverviewHandler(ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IExamBlueprintRepository examBlueprintRepository, IQuestionRepository questionRepository, ISessionRepository sessionRepository, ILessonRepository lessonRepository, ILessonOpeningRepository lessonOpeningRepository, IRuntimeSettings runtimeSettings, ICurrentUserService currentUserService) : IRequestHandler<GetUnitExamOverviewQuery, UnitExamOverviewResult>
{
    public async Task<UnitExamOverviewResult> Handle(GetUnitExamOverviewQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var unit = await unitRepository.GetByIdAsync(request.UnitId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (unit is null)
        {
            throw new NotFoundCoreException(ErrorCodes.UnitNotFound);
        }

        var subject = await subjectRepository.GetByIdAsync(unit.SubjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (subject is null)
        {
            throw new NotFoundCoreException(ErrorCodes.UnitNotFound);
        }

        var blueprints = await examBlueprintRepository.FindAsync(x => x.SubjectId == unit.SubjectId && (x.UnitId == unit.Id || x.UnitId == null), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var blueprint = ExamBlueprintResolution.ForUnit(blueprints, unit);
        var available = ServableTypeCounts.ForUnit(await questionRepository.CountServableByUnitAndTypeAsync(unit.SubjectId, cancellationToken).ConfigureAwait(false), unit.Id);
        var openExam = await sessionRepository.FirstOrDefaultAsync(x => x.StudentId == userId && x.Kind != SessionKind.Quiz && x.SubmittedAt == null, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var isTestMode = currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Admin);
        var requireAllLessonsOpened = await runtimeSettings.GetAsync(FeatureFlagRuntimeSettings.ExamsRequireAllLessonsOpened, cancellationToken).ConfigureAwait(false);
        var unopened = requireAllLessonsOpened && !isTestMode ? await ExamLessonGate.CountUnopenedAsync(userId, [unit.Id], lessonRepository, lessonOpeningRepository, cancellationToken).ConfigureAwait(false) : 0;
        return UnitExamOverviewResultGenerator.Generate(unit, subject, blueprint, available, openExam, unopened);
    }
}
