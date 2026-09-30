using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Dashboard.GetSolveRateMetrics;

public sealed class GetSolveRateMetricsValidator : AbstractValidator<GetSolveRateMetricsQuery>
{
    public GetSolveRateMetricsValidator(IOptions<DashboardOptions> dashboardOptions, TimeProvider timeProvider)
    {
        this.AddDashboardFilterRules(dashboardOptions.Value, timeProvider);
    }
}
