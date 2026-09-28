using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Units.CreateUnit;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Units.CreateUnit;

public sealed class CreateUnitValidatorTests
{
    private readonly CreateUnitValidator _validator = new(Options.Create(new ContentOptions { SubjectNameMaxLength = 100, UnitNameMaxLength = 100 }));

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new CreateUnitCommand(Guid.NewGuid(), "Mechanics"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySubjectId_FailsWithSubjectIdRequired()
    {
        var result = _validator.Validate(new CreateUnitCommand(Guid.Empty, "Mechanics"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectIdRequired);
    }

    [Fact]
    public void Validate_EmptyName_FailsWithUnitNameRequired()
    {
        var result = _validator.Validate(new CreateUnitCommand(Guid.NewGuid(), string.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UnitNameRequired);
    }

    [Fact]
    public void Validate_NameOverMax_FailsWithUnitNameTooLong()
    {
        var result = _validator.Validate(new CreateUnitCommand(Guid.NewGuid(), new string('a', 101)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UnitNameTooLong);
    }
}
