using Elmanhg.Application.Dashboard.GetMyTeacherStats;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Dashboard.GetMyTeacherStats;

public sealed class GetMyTeacherStatsValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private readonly GetMyTeacherStatsValidator _validator;

    public GetMyTeacherStatsValidatorTests()
    {
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(Now);
        _validator = new GetMyTeacherStatsValidator(Options.Create(new DashboardOptions()), clock);
    }

    [Fact]
    public void Validate_NoDates_Passes()
    {
        _validator.Validate(new GetMyTeacherStatsQuery(null, null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_FromAfterTo_FailsWithDashboardDateRangeInvalid()
    {
        var result = _validator.Validate(new GetMyTeacherStatsQuery(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 5)));

        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(ErrorCodes.DashboardDateRangeInvalid);
    }

    [Fact]
    public void Validate_RangeOverMax_FailsWithDashboardDateRangeTooWide()
    {
        var result = _validator.Validate(new GetMyTeacherStatsQuery(new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 10)));

        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(ErrorCodes.DashboardDateRangeTooWide);
    }
}
