using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Progress.GetStudentProgress;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Progress.GetStudentProgress;

public sealed class GetStudentProgressValidatorTests
{
    private readonly GetStudentProgressValidator _validator = new();

    [Fact]
    public void Validate_StudentId_Passes()
    {
        var result = _validator.Validate(new GetStudentProgressQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyStudentId_FailsWithStudentIdRequired()
    {
        var result = _validator.Validate(new GetStudentProgressQuery(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.StudentIdRequired);
    }
}
