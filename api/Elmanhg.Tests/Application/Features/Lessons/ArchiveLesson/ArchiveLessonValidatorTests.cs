using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.ArchiveLesson;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Lessons.ArchiveLesson;

public sealed class ArchiveLessonValidatorTests
{
    private readonly ArchiveLessonValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new ArchiveLessonCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyLessonId_FailsLessonIdRequired()
    {
        var result = _validator.Validate(new ArchiveLessonCommand(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonIdRequired);
    }
}
