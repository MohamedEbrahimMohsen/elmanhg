using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Teachers.AssignTeacherSubject;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Teachers.AssignTeacherSubject;

public sealed class AssignTeacherSubjectValidatorTests
{
    private readonly AssignTeacherSubjectValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new AssignTeacherSubjectCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyTeacherId_FailsWithTeacherIdRequired()
    {
        var result = _validator.Validate(new AssignTeacherSubjectCommand(Guid.Empty, Guid.NewGuid()));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherIdRequired);
    }

    [Fact]
    public void Validate_EmptySubjectId_FailsWithSubjectIdRequired()
    {
        var result = _validator.Validate(new AssignTeacherSubjectCommand(Guid.NewGuid(), Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectIdRequired);
    }
}
