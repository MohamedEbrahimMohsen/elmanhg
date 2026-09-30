using Elmanhg.Application.Dashboard.GetAskTeacherMetrics;
using Elmanhg.Application.Dashboard.GetFunnelMetrics;
using Elmanhg.Application.Dashboard.GetPaymentMetrics;
using Elmanhg.Application.Dashboard.GetSolveRateMetrics;
using Elmanhg.Application.Dashboard.GetStudentMetrics;
using Elmanhg.Application.Dashboard.GetSubscriberMetrics;
using Elmanhg.Application.Dashboard.GetSuccessRateMetrics;
using Elmanhg.Application.Dashboard.GetValidationMetrics;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Dashboard.Shared;

public sealed class DashboardFilterRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Later = new(2026, 1, 10);
    private static readonly DateOnly Earlier = new(2026, 1, 5);
    private readonly GetStudentMetricsValidator _validator = new(Options.Create(new DashboardOptions()), Clock());

    public static TheoryData<IValidator, object> RangeValidators()
    {
        var options = Options.Create(new DashboardOptions());
        var clock = Clock();
        return new TheoryData<IValidator, object>
        {
            { new GetStudentMetricsValidator(options, clock), new GetStudentMetricsQuery(Later, Earlier) },
            { new GetSubscriberMetricsValidator(options, clock), new GetSubscriberMetricsQuery(Later, Earlier) },
            { new GetSolveRateMetricsValidator(options, clock), new GetSolveRateMetricsQuery(Later, Earlier, null) },
            { new GetSuccessRateMetricsValidator(options, clock), new GetSuccessRateMetricsQuery(Later, Earlier, null) },
            { new GetValidationMetricsValidator(options, clock), new GetValidationMetricsQuery(Later, Earlier, null) },
            { new GetAskTeacherMetricsValidator(options, clock), new GetAskTeacherMetricsQuery(Later, Earlier, null) },
            { new GetPaymentMetricsValidator(options, clock), new GetPaymentMetricsQuery(Later, Earlier) },
            { new GetFunnelMetricsValidator(options, clock), new GetFunnelMetricsQuery(Later, Earlier) },
        };
    }

    [Fact]
    public void Validate_NoDates_Passes()
    {
        _validator.Validate(new GetStudentMetricsQuery(null, null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_FromAfterTo_FailsWithDashboardDateRangeInvalid()
    {
        var result = _validator.Validate(new GetStudentMetricsQuery(Later, Earlier));

        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(ErrorCodes.DashboardDateRangeInvalid);
    }

    [Fact]
    public void Validate_FromAfterTodayWithoutTo_FailsWithDashboardDateRangeInvalid()
    {
        var result = _validator.Validate(new GetStudentMetricsQuery(new DateOnly(2026, 1, 16), null));

        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(ErrorCodes.DashboardDateRangeInvalid);
    }

    [Fact]
    public void Validate_RangeOfMaxDays_Passes()
    {
        var to = new DateOnly(2026, 1, 15);

        _validator.Validate(new GetStudentMetricsQuery(to.AddDays(-365), to)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RangeLongerThanMax_FailsWithDashboardDateRangeTooWide()
    {
        var to = new DateOnly(2026, 1, 15);

        var result = _validator.Validate(new GetStudentMetricsQuery(to.AddDays(-366), to));

        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(ErrorCodes.DashboardDateRangeTooWide);
    }

    [Theory]
    [MemberData(nameof(RangeValidators))]
    public void Validate_EveryRangeValidator_FromAfterTo_FailsWithDashboardDateRangeInvalid(IValidator validator, object query)
    {
        var result = validator.Validate(new ValidationContext<object>(query));

        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(ErrorCodes.DashboardDateRangeInvalid);
    }

    private static TimeProvider Clock()
    {
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(Now);
        return clock;
    }
}
