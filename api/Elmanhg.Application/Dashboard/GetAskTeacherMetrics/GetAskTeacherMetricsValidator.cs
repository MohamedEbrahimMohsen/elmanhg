using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Dashboard.GetAskTeacherMetrics;

public sealed class GetAskTeacherMetricsValidator : AbstractValidator<GetAskTeacherMetricsQuery>
{
    public GetAskTeacherMetricsValidator(IOptions<DashboardOptions> dashboardOptions, TimeProvider timeProvider)
    {
        this.AddDashboardFilterRules(dashboardOptions.Value, timeProvider);
    }
}
