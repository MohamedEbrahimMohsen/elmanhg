using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Dashboard.GetSubscriberMetrics;

public sealed class GetSubscriberMetricsValidator : AbstractValidator<GetSubscriberMetricsQuery>
{
    public GetSubscriberMetricsValidator(IOptions<DashboardOptions> dashboardOptions, TimeProvider timeProvider)
    {
        this.AddDashboardFilterRules(dashboardOptions.Value, timeProvider);
    }
}
