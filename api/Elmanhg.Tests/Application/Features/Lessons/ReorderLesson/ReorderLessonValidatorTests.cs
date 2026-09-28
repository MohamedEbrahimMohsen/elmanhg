using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.ReorderLesson;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Lessons.ReorderLesson;

public sealed class ReorderLessonValidatorTests
{
    private readonly ReorderLessonValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new ReorderLessonCommand(Guid.NewGuid(), 1));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyLessonId_FailsLessonIdRequired()
    {
        var result = _validator.Validate(new ReorderLessonCommand(Guid.Empty, 1));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonIdRequired);
    }

    [Fact]
    public void Validate_PositionZero_FailsLessonPositionInvalid()
    {
        var result = _validator.Validate(new ReorderLessonCommand(Guid.NewGuid(), 0));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonPositionInvalid);
    }
}
