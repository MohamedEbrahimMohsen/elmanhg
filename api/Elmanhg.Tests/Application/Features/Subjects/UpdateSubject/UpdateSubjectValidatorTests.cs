using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subjects.UpdateSubject;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Subjects.UpdateSubject;

public sealed class UpdateSubjectValidatorTests
{
    private readonly UpdateSubjectValidator _validator = new(Options.Create(new ContentOptions { SubjectNameMaxLength = 100, UnitNameMaxLength = 100 }));

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new UpdateSubjectCommand(Guid.NewGuid(), "Physics"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySubjectId_FailsWithSubjectIdRequired()
    {
        var result = _validator.Validate(new UpdateSubjectCommand(Guid.Empty, "Physics"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectIdRequired);
    }

    [Fact]
    public void Validate_EmptyName_FailsWithSubjectNameRequired()
    {
        var result = _validator.Validate(new UpdateSubjectCommand(Guid.NewGuid(), string.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectNameRequired);
    }

    [Fact]
    public void Validate_NameOverMax_FailsWithSubjectNameTooLong()
    {
        var result = _validator.Validate(new UpdateSubjectCommand(Guid.NewGuid(), new string('a', 101)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectNameTooLong);
    }
}
