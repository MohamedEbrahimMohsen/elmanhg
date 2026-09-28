using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subjects.DeleteSubject;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Subjects.DeleteSubject;

public sealed class DeleteSubjectValidatorTests
{
    private readonly DeleteSubjectValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new DeleteSubjectCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySubjectId_FailsWithSubjectIdRequired()
    {
        var result = _validator.Validate(new DeleteSubjectCommand(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectIdRequired);
    }
}
