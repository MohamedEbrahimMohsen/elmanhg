using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Students.GetStudentProfile;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Students.GetStudentProfile;

public sealed class GetStudentProfileValidatorTests
{
    private readonly GetStudentProfileValidator _validator = new();

    [Fact]
    public void Validate_StudentId_Passes()
    {
        var result = _validator.Validate(new GetStudentProfileQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyStudentId_FailsWithStudentIdRequired()
    {
        var result = _validator.Validate(new GetStudentProfileQuery(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.StudentIdRequired);
    }
}
