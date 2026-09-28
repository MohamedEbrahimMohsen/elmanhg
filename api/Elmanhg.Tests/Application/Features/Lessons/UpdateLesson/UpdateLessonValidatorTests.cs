using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.UpdateLesson;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Lessons.UpdateLesson;

public sealed class UpdateLessonValidatorTests
{
    private readonly UpdateLessonValidator _validator = new(Options.Create(new ContentOptions { SubjectNameMaxLength = 100, UnitNameMaxLength = 100, LessonNameMaxLength = 100, LessonExplanationMaxLength = 100000, LessonSummaryMaxLength = 20000, LessonObjectiveMaxLength = 300, LessonObjectivesMaxCount = 20, LessonVideoUrlMaxLength = 2048, LessonImageMaxSizeInMb = 5 }));

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(Command());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NullVideoUrlAndEmptyContent_Passes()
    {
        var result = _validator.Validate(Command() with { Explanation = null, Summary = string.Empty, VideoUrl = null, Objectives = [] });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyLessonId_FailsWithLessonIdRequired()
    {
        var result = _validator.Validate(Command() with { LessonId = Guid.Empty });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonIdRequired);
    }

    [Fact]
    public void Validate_EmptyName_FailsWithLessonNameRequired()
    {
        var result = _validator.Validate(Command() with { Name = string.Empty });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonNameRequired);
    }

    [Fact]
    public void Validate_NameOverMax_FailsWithLessonNameTooLong()
    {
        var result = _validator.Validate(Command() with { Name = new string('a', 101) });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonNameTooLong);
    }

    [Fact]
    public void Validate_ExplanationOverMax_FailsWithLessonExplanationTooLong()
    {
        var result = _validator.Validate(Command() with { Explanation = new string('a', 100001) });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonExplanationTooLong);
    }

    [Fact]
    public void Validate_SummaryOverMax_FailsWithLessonSummaryTooLong()
    {
        var result = _validator.Validate(Command() with { Summary = new string('a', 20001) });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonSummaryTooLong);
    }

    [Fact]
    public void Validate_VideoUrlNotHttp_FailsWithLessonVideoUrlInvalid()
    {
        var result = _validator.Validate(Command() with { VideoUrl = "javascript:alert(1)" });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonVideoUrlInvalid);
    }

    [Fact]
    public void Validate_VideoUrlOverMax_FailsWithLessonVideoUrlTooLong()
    {
        var result = _validator.Validate(Command() with { VideoUrl = "https://example.com/" + new string('a', 2030) });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonVideoUrlTooLong);
    }

    [Fact]
    public void Validate_TooManyObjectives_FailsWithLessonObjectivesTooMany()
    {
        var objectives = Enumerable.Range(1, 21).Select(x => new LessonObjectiveContent(null, $"Objective {x}")).ToList();

        var result = _validator.Validate(Command() with { Objectives = objectives });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonObjectivesTooMany);
    }

    [Fact]
    public void Validate_EmptyObjectiveText_FailsWithLessonObjectiveTextRequired()
    {
        var result = _validator.Validate(Command() with { Objectives = [new LessonObjectiveContent(null, string.Empty)] });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonObjectiveTextRequired);
    }

    [Fact]
    public void Validate_ObjectiveTextOverMax_FailsWithLessonObjectiveTextTooLong()
    {
        var result = _validator.Validate(Command() with { Objectives = [new LessonObjectiveContent(null, new string('a', 301))] });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonObjectiveTextTooLong);
    }

    [Fact]
    public void Validate_DuplicateObjectiveIds_FailsWithLessonObjectiveDuplicate()
    {
        var id = Guid.NewGuid();

        var result = _validator.Validate(Command() with { Objectives = [new LessonObjectiveContent(id, "First"), new LessonObjectiveContent(id, "Second")] });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonObjectiveDuplicate);
    }

    private static UpdateLessonCommand Command()
    {
        return new UpdateLessonCommand(Guid.NewGuid(), "Newton's laws", "<p>Force</p>", "<p>Summary</p>", "https://example.com/video", [new LessonObjectiveContent(null, "State the first law")]);
    }
}
