using Elmanhg.Application.Exams.GetUnitExamAttempts;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Exams.GetUnitExamAttempts;

public sealed class GetUnitExamAttemptsValidatorTests
{
    private readonly GetUnitExamAttemptsValidator _validator = new();

    [Fact]
    public void Validate_EmptyUnitId_ReturnsUnitIdRequired()
    {
        _validator.Validate(new GetUnitExamAttemptsQuery(Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UnitIdRequired);
    }

    [Fact]
    public void Validate_UnitId_IsValid()
    {
        _validator.Validate(new GetUnitExamAttemptsQuery(Guid.NewGuid())).IsValid.Should().BeTrue();
    }
}
