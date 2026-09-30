using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Dashboard.GetSuccessRateMetrics;

public sealed class GetSuccessRateMetricsHandler(ISessionRepository sessionRepository, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, TimeProvider timeProvider, IOptions<DashboardOptions> dashboardOptions, IOptions<MasteryOptions> masteryOptions) : IRequestHandler<GetSuccessRateMetricsQuery, SuccessRateMetricsResult>
{
    public async Task<SuccessRateMetricsResult> Handle(GetSuccessRateMetricsQuery request, CancellationToken cancellationToken)
    {
        await DashboardSubjectGuard.EnsureExistsAsync(request.SubjectId, subjectRepository, cancellationToken).ConfigureAwait(false);
        var window = DashboardWindow.Resolve(request.From, request.To, timeProvider.GetUtcNow(), dashboardOptions.Value);
        var outcomes = await sessionRepository.GetAttemptOutcomesByLessonAsync(window.Start, window.End, request.SubjectId, masteryOptions.Value.CorrectThreshold, cancellationToken).ConfigureAwait(false);
        var subjectIds = outcomes.Select(x => x.SubjectId).Distinct().ToList();
        var unitIds = outcomes.Select(x => x.UnitId).Distinct().ToList();
        var lessonIds = outcomes.Select(x => x.LessonId).Distinct().ToList();
        List<Subject> subjects = subjectIds.Count == 0 ? [] : await subjectRepository.FindAsync(x => subjectIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        List<CurriculumUnit> units = unitIds.Count == 0 ? [] : await unitRepository.FindAsync(x => unitIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        List<Lesson> lessons = lessonIds.Count == 0 ? [] : await lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);

        return SuccessRateMetricsResultGenerator.Generate(window, request.SubjectId, outcomes, subjects, units, lessons);
    }
}
