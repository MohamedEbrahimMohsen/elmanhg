using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subjects.ReorderSubject;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Subjects.ReorderSubject;

public sealed class ReorderSubjectValidatorTests
{
    private readonly ReorderSubjectValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new ReorderSubjectCommand(Guid.NewGuid(), 1));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySubjectId_FailsWithSubjectIdRequired()
    {
        var result = _validator.Validate(new ReorderSubjectCommand(Guid.Empty, 1));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectIdRequired);
    }

    [Fact]
    public void Validate_PositionZero_FailsWithSubjectPositionInvalid()
    {
        var result = _validator.Validate(new ReorderSubjectCommand(Guid.NewGuid(), 0));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectPositionInvalid);
    }
}
