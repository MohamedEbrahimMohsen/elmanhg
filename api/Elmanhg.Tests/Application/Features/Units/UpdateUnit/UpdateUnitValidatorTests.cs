using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Units.UpdateUnit;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Units.UpdateUnit;

public sealed class UpdateUnitValidatorTests
{
    private readonly UpdateUnitValidator _validator = new(Options.Create(new ContentOptions { SubjectNameMaxLength = 100, UnitNameMaxLength = 100 }));

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new UpdateUnitCommand(Guid.NewGuid(), Guid.NewGuid(), "Mechanics"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySubjectId_FailsWithSubjectIdRequired()
    {
        var result = _validator.Validate(new UpdateUnitCommand(Guid.Empty, Guid.NewGuid(), "Mechanics"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectIdRequired);
    }

    [Fact]
    public void Validate_EmptyUnitId_FailsWithUnitIdRequired()
    {
        var result = _validator.Validate(new UpdateUnitCommand(Guid.NewGuid(), Guid.Empty, "Mechanics"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UnitIdRequired);
    }

    [Fact]
    public void Validate_EmptyName_FailsWithUnitNameRequired()
    {
        var result = _validator.Validate(new UpdateUnitCommand(Guid.NewGuid(), Guid.NewGuid(), string.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UnitNameRequired);
    }

    [Fact]
    public void Validate_NameOverMax_FailsWithUnitNameTooLong()
    {
        var result = _validator.Validate(new UpdateUnitCommand(Guid.NewGuid(), Guid.NewGuid(), new string('a', 101)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UnitNameTooLong);
    }
}
