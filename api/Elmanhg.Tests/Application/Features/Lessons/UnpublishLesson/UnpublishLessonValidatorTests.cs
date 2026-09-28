using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.UnpublishLesson;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Lessons.UnpublishLesson;

public sealed class UnpublishLessonValidatorTests
{
    private readonly UnpublishLessonValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new UnpublishLessonCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyLessonId_FailsLessonIdRequired()
    {
        var result = _validator.Validate(new UnpublishLessonCommand(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonIdRequired);
    }
}
