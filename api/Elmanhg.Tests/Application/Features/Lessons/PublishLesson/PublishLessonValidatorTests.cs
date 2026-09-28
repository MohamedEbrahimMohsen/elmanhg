using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.PublishLesson;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Lessons.PublishLesson;

public sealed class PublishLessonValidatorTests
{
    private readonly PublishLessonValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new PublishLessonCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyLessonId_FailsLessonIdRequired()
    {
        var result = _validator.Validate(new PublishLessonCommand(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonIdRequired);
    }
}
