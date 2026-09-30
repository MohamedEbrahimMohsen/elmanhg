using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Analytics;
using Elmanhg.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Dashboard.GetStudentMetrics;

public sealed class GetStudentMetricsHandler(IUserRepository userRepository, IUserActivityDayRepository userActivityDayRepository, TimeProvider timeProvider, IOptions<DashboardOptions> dashboardOptions) : IRequestHandler<GetStudentMetricsQuery, StudentMetricsResult>
{
    public async Task<StudentMetricsResult> Handle(GetStudentMetricsQuery request, CancellationToken cancellationToken)
    {
        var options = dashboardOptions.Value;
        var window = DashboardWindow.Resolve(request.From, request.To, timeProvider.GetUtcNow(), options);
        var weekStart = window.StartOfDay(window.Today.AddDays(-(options.RecentWeekDays - 1)));
        var total = await userRepository.CountAsync(cancellationToken, x => x.Role == UserRole.Student).ConfigureAwait(false);
        var newInRange = await userRepository.CountAsync(cancellationToken, x => x.Role == UserRole.Student && x.CreationDate >= window.Start && x.CreationDate < window.End).ConfigureAwait(false);
        var newThisWeek = await userRepository.CountAsync(cancellationToken, x => x.Role == UserRole.Student && x.CreationDate >= weekStart).ConfigureAwait(false);
        var activeToday = await userActivityDayRepository.CountActiveStudentsAsync(window.Today, window.Today, cancellationToken).ConfigureAwait(false);
        var activeThisMonth = await userActivityDayRepository.CountActiveStudentsAsync(window.Today.AddDays(-(options.RecentMonthDays - 1)), window.Today, cancellationToken).ConfigureAwait(false);
        var daily = await userActivityDayRepository.CountActiveStudentsByDayAsync(window.From, window.To, cancellationToken).ConfigureAwait(false);

        return new StudentMetricsResult(window.From, window.To, total, newInRange, newThisWeek, activeToday, activeThisMonth, DashboardSeries.Fill(window, daily), window.Now);
    }
}
