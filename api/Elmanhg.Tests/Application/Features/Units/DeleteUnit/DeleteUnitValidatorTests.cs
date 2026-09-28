using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Units.DeleteUnit;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Units.DeleteUnit;

public sealed class DeleteUnitValidatorTests
{
    private readonly DeleteUnitValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new DeleteUnitCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySubjectId_FailsWithSubjectIdRequired()
    {
        var result = _validator.Validate(new DeleteUnitCommand(Guid.Empty, Guid.NewGuid()));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectIdRequired);
    }

    [Fact]
    public void Validate_EmptyUnitId_FailsWithUnitIdRequired()
    {
        var result = _validator.Validate(new DeleteUnitCommand(Guid.NewGuid(), Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UnitIdRequired);
    }
}
