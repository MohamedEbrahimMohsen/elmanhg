using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Dashboard.GetMyTeacherStats;

public sealed class GetMyTeacherStatsValidator : AbstractValidator<GetMyTeacherStatsQuery>
{
    public GetMyTeacherStatsValidator(IOptions<DashboardOptions> dashboardOptions, TimeProvider timeProvider)
    {
        this.AddDashboardFilterRules(dashboardOptions.Value, timeProvider);
    }
}
