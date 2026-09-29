using Elmanhg.Application.Browse.GetStudentLesson;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Browse.GetStudentLesson;

public sealed class GetStudentLessonValidatorTests
{
    private readonly GetStudentLessonValidator _validator = new();

    [Fact]
    public void Validate_LessonId_Passes()
    {
        var result = _validator.Validate(new GetStudentLessonQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyLessonId_FailsWithLessonIdRequired()
    {
        var result = _validator.Validate(new GetStudentLessonQuery(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonIdRequired);
    }
}
