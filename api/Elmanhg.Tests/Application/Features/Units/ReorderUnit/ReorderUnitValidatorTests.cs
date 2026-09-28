using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Units.ReorderUnit;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Units.ReorderUnit;

public sealed class ReorderUnitValidatorTests
{
    private readonly ReorderUnitValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new ReorderUnitCommand(Guid.NewGuid(), Guid.NewGuid(), 1));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySubjectId_FailsWithSubjectIdRequired()
    {
        var result = _validator.Validate(new ReorderUnitCommand(Guid.Empty, Guid.NewGuid(), 1));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectIdRequired);
    }

    [Fact]
    public void Validate_EmptyUnitId_FailsWithUnitIdRequired()
    {
        var result = _validator.Validate(new ReorderUnitCommand(Guid.NewGuid(), Guid.Empty, 1));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UnitIdRequired);
    }

    [Fact]
    public void Validate_PositionZero_FailsWithUnitPositionInvalid()
    {
        var result = _validator.Validate(new ReorderUnitCommand(Guid.NewGuid(), Guid.NewGuid(), 0));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UnitPositionInvalid);
    }
}
