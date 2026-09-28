using Elmanhg.Application.Exams.GetUnitExamOverview;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Exams.GetUnitExamOverview;

public sealed class GetUnitExamOverviewValidatorTests
{
    private readonly GetUnitExamOverviewValidator _validator = new();

    [Fact]
    public void Validate_ValidId_Passes()
    {
        _validator.Validate(new GetUnitExamOverviewQuery(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyUnitId_FailsWithUnitIdRequired()
    {
        _validator.Validate(new GetUnitExamOverviewQuery(Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UnitIdRequired);
    }
}
