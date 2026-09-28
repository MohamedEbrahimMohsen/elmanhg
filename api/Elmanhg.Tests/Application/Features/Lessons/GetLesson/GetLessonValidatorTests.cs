using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.GetLesson;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Lessons.GetLesson;

public sealed class GetLessonValidatorTests
{
    private readonly GetLessonValidator _validator = new();

    [Fact]
    public void Validate_ValidQuery_Passes()
    {
        var result = _validator.Validate(new GetLessonQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyLessonId_FailsWithLessonIdRequired()
    {
        var result = _validator.Validate(new GetLessonQuery(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonIdRequired);
    }
}
