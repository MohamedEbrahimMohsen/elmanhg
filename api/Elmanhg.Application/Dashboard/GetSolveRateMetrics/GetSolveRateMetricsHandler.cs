using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Analytics;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Dashboard.GetSolveRateMetrics;

public sealed class GetSolveRateMetricsHandler(ISessionRepository sessionRepository, IUserActivityDayRepository userActivityDayRepository, ISubjectRepository subjectRepository, TimeProvider timeProvider, IOptions<DashboardOptions> dashboardOptions) : IRequestHandler<GetSolveRateMetricsQuery, SolveRateMetricsResult>
{
    public async Task<SolveRateMetricsResult> Handle(GetSolveRateMetricsQuery request, CancellationToken cancellationToken)
    {
        await DashboardSubjectGuard.EnsureExistsAsync(request.SubjectId, subjectRepository, cancellationToken).ConfigureAwait(false);
        var window = DashboardWindow.Resolve(request.From, request.To, timeProvider.GetUtcNow(), dashboardOptions.Value);
        var attempts = await sessionRepository.CountAttemptsByDayAsync(window.ToMetricsWindow(), request.SubjectId, cancellationToken).ConfigureAwait(false);
        var activeStudents = await userActivityDayRepository.CountActiveStudentsByDayAsync(window.From, window.To, cancellationToken).ConfigureAwait(false);

        return SolveRateMetricsResultGenerator.Generate(window, request.SubjectId, attempts, activeStudents);
    }
}
