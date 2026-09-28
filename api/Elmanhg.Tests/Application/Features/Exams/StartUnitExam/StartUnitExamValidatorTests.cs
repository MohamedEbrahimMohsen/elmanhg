using Elmanhg.Application.Exams.StartUnitExam;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Exams.StartUnitExam;

public sealed class StartUnitExamValidatorTests
{
    private readonly StartUnitExamValidator _validator = new();

    [Fact]
    public void Validate_ValidId_Passes()
    {
        _validator.Validate(new StartUnitExamCommand(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyUnitId_FailsWithUnitIdRequired()
    {
        _validator.Validate(new StartUnitExamCommand(Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UnitIdRequired);
    }
}
