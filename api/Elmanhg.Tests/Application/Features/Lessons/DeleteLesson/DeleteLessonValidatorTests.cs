using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.DeleteLesson;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Lessons.DeleteLesson;

public sealed class DeleteLessonValidatorTests
{
    private readonly DeleteLessonValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new DeleteLessonCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyLessonId_FailsLessonIdRequired()
    {
        var result = _validator.Validate(new DeleteLessonCommand(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonIdRequired);
    }
}
