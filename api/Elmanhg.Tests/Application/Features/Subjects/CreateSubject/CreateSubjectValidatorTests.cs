using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subjects.CreateSubject;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Subjects.CreateSubject;

public sealed class CreateSubjectValidatorTests
{
    private readonly CreateSubjectValidator _validator = new(Options.Create(new ContentOptions { SubjectNameMaxLength = 100, UnitNameMaxLength = 100 }));

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new CreateSubjectCommand("Physics"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyName_FailsWithSubjectNameRequired()
    {
        var result = _validator.Validate(new CreateSubjectCommand(string.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectNameRequired);
    }

    [Fact]
    public void Validate_WhitespaceName_FailsWithSubjectNameRequired()
    {
        var result = _validator.Validate(new CreateSubjectCommand("   "));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectNameRequired);
    }

    [Fact]
    public void Validate_NameOverMax_FailsWithSubjectNameTooLong()
    {
        var result = _validator.Validate(new CreateSubjectCommand(new string('a', 101)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectNameTooLong);
    }
}
