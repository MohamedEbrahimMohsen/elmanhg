using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Dashboard.GetPaymentMetrics;

public sealed class GetPaymentMetricsValidator : AbstractValidator<GetPaymentMetricsQuery>
{
    public GetPaymentMetricsValidator(IOptions<DashboardOptions> dashboardOptions, TimeProvider timeProvider)
    {
        this.AddDashboardFilterRules(dashboardOptions.Value, timeProvider);
    }
}
