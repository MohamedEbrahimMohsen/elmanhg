using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Application.Features.Questions.Shared;

public sealed class QuestionFieldsValidatorTests
{
    private readonly QuestionFieldsValidator _validator = new(Options.Create(new ContentOptions { SubjectNameMaxLength = 100, UnitNameMaxLength = 100, LessonNameMaxLength = 100, LessonExplanationMaxLength = 100000, LessonSummaryMaxLength = 20000, LessonObjectiveMaxLength = 300, LessonObjectivesMaxCount = 20, LessonVideoUrlMaxLength = 2048, LessonImageMaxSizeInMb = 5, QuestionStemMaxLength = 20000, QuestionExplanationMaxLength = 20000, QuestionOptionsMaxCount = 10, QuestionOptionTextMaxLength = 2000, QuestionBlanksMaxCount = 10, QuestionAcceptedAnswersMaxCount = 20, QuestionAnswerMaxLength = 200, QuestionTagsMaxCount = 10, QuestionTagMaxLength = 50, QuestionMaxScoreMax = 100 }));

    internal static QuestionFields ValidMcq() => new(QuestionType.Mcq, "<p>2 + 2 = ?</p>", Json("""{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}"""), Json("""{"correctOptionId":"b"}"""), "<p>Add.</p>", QuestionDifficulty.Medium, null, ["arithmetic"], 1);

    [Fact]
    public void Validate_ValidMcq_HasNoErrors()
    {
        _validator.Validate(ValidMcq()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingType_HasQuestionTypeRequired()
    {
        var codes = Codes(ValidMcq() with { Type = null, Body = Json("[]") });

        codes.Should().Contain(ErrorCodes.QuestionTypeRequired).And.NotContain(ErrorCodes.QuestionBodyInvalid);
    }

    [Fact]
    public void Validate_UndefinedType_HasQuestionTypeInvalid()
    {
        Codes(ValidMcq() with { Type = (QuestionType)99 }).Should().Contain(ErrorCodes.QuestionTypeInvalid);
    }

    [Fact]
    public void Validate_EmptyStem_HasQuestionStemRequired()
    {
        Codes(ValidMcq() with { Stem = string.Empty }).Should().Contain(ErrorCodes.QuestionStemRequired);
    }

    [Fact]
    public void Validate_StemOverCap_HasQuestionStemTooLong()
    {
        Codes(ValidMcq() with { Stem = new string('a', 20001) }).Should().Contain(ErrorCodes.QuestionStemTooLong);
    }

    [Fact]
    public void Validate_ExplanationOverCap_HasQuestionExplanationTooLong()
    {
        Codes(ValidMcq() with { Explanation = new string('a', 20001) }).Should().Contain(ErrorCodes.QuestionExplanationTooLong);
    }

    [Fact]
    public void Validate_MissingDifficulty_HasQuestionDifficultyRequired()
    {
        Codes(ValidMcq() with { Difficulty = null }).Should().Contain(ErrorCodes.QuestionDifficultyRequired);
    }

    [Fact]
    public void Validate_UndefinedDifficulty_HasQuestionDifficultyInvalid()
    {
        Codes(ValidMcq() with { Difficulty = (QuestionDifficulty)9 }).Should().Contain(ErrorCodes.QuestionDifficultyInvalid);
    }

    [Fact]
    public void Validate_MissingMaxScore_HasQuestionMaxScoreRequired()
    {
        Codes(ValidMcq() with { MaxScore = null }).Should().Contain(ErrorCodes.QuestionMaxScoreRequired);
    }

    [Fact]
    public void Validate_MaxScoreZero_HasQuestionMaxScoreInvalid()
    {
        Codes(ValidMcq() with { MaxScore = 0 }).Should().Contain(ErrorCodes.QuestionMaxScoreInvalid);
    }

    [Fact]
    public void Validate_MaxScoreOverCap_HasQuestionMaxScoreInvalid()
    {
        Codes(ValidMcq() with { MaxScore = 101 }).Should().Contain(ErrorCodes.QuestionMaxScoreInvalid);
    }

    [Fact]
    public void Validate_TooManyTags_HasQuestionTagsTooMany()
    {
        Codes(ValidMcq() with { Tags = Enumerable.Range(1, 11).Select(x => $"tag{x}").ToList() }).Should().Contain(ErrorCodes.QuestionTagsTooMany);
    }

    [Fact]
    public void Validate_BlankTag_HasQuestionTagRequired()
    {
        Codes(ValidMcq() with { Tags = ["arithmetic", " "] }).Should().Contain(ErrorCodes.QuestionTagRequired);
    }

    [Fact]
    public void Validate_TagOverCap_HasQuestionTagTooLong()
    {
        Codes(ValidMcq() with { Tags = [new string('a', 51)] }).Should().Contain(ErrorCodes.QuestionTagTooLong);
    }

    [Fact]
    public void Validate_SchemaViolation_SurfacesTypeRuleCode()
    {
        Codes(ValidMcq() with { GradingSpec = Json("""{"correctOptionId":"z"}""") }).Should().Contain(ErrorCodes.QuestionCorrectOptionInvalid);
    }

    private List<string> Codes(QuestionFields fields)
    {
        return _validator.Validate(fields).Errors
            .Select(x => x.ErrorCode)
            .ToList();
    }
}
