using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;

namespace Elmanhg.Application.Dashboard.Shared;

public static class DashboardFilterRules
{
    public static void AddDashboardFilterRules<T>(this AbstractValidator<T> validator, DashboardOptions options, TimeProvider timeProvider) where T : IDashboardRange
    {
        DashboardWindow Resolve(T query) => DashboardWindow.Resolve(query.From, query.To, timeProvider.GetUtcNow(), options);

        validator.RuleFor(x => x)
            .Must(x => Resolve(x).From <= Resolve(x).To)
            .WithErrorCode(ErrorCodes.DashboardDateRangeInvalid);

        validator.RuleFor(x => x)
            .Must(x => Resolve(x).DayCount <= options.MaxRangeDays)
            .WithErrorCode(ErrorCodes.DashboardDateRangeTooWide);
    }
}
