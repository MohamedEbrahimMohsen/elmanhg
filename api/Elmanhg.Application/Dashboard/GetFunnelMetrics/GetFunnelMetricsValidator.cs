using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Dashboard.GetFunnelMetrics;

public sealed class GetFunnelMetricsValidator : AbstractValidator<GetFunnelMetricsQuery>
{
    public GetFunnelMetricsValidator(IOptions<DashboardOptions> dashboardOptions, TimeProvider timeProvider)
    {
        this.AddDashboardFilterRules(dashboardOptions.Value, timeProvider);
    }
}
