using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.CreateLesson;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Lessons.CreateLesson;

public sealed class CreateLessonValidatorTests
{
    private readonly CreateLessonValidator _validator = new(Options.Create(new ContentOptions { SubjectNameMaxLength = 100, UnitNameMaxLength = 100, LessonNameMaxLength = 100, LessonExplanationMaxLength = 100000, LessonSummaryMaxLength = 20000, LessonObjectiveMaxLength = 300, LessonObjectivesMaxCount = 20, LessonVideoUrlMaxLength = 2048, LessonImageMaxSizeInMb = 5 }));

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new CreateLessonCommand(Guid.NewGuid(), "Newton's laws"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyUnitId_FailsWithUnitIdRequired()
    {
        var result = _validator.Validate(new CreateLessonCommand(Guid.Empty, "Newton's laws"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UnitIdRequired);
    }

    [Fact]
    public void Validate_EmptyName_FailsWithLessonNameRequired()
    {
        var result = _validator.Validate(new CreateLessonCommand(Guid.NewGuid(), string.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonNameRequired);
    }

    [Fact]
    public void Validate_NameOverMax_FailsWithLessonNameTooLong()
    {
        var result = _validator.Validate(new CreateLessonCommand(Guid.NewGuid(), new string('a', 101)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonNameTooLong);
    }
}
