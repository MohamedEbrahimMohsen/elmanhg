using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using System.Text.Json.Nodes;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Application.Features.Questions.Shared;

public sealed class ShortQuestionRulesTests
{
    private const string Numeric = """{"answerKind":"numeric"}""";
    private const string Text = """{"answerKind":"text"}""";
    private readonly ContentOptions _options = new() { SubjectNameMaxLength = 100, UnitNameMaxLength = 100, LessonNameMaxLength = 100, LessonExplanationMaxLength = 100000, LessonSummaryMaxLength = 20000, LessonObjectiveMaxLength = 300, LessonObjectivesMaxCount = 20, LessonVideoUrlMaxLength = 2048, LessonImageMaxSizeInMb = 5, QuestionStemMaxLength = 20000, QuestionExplanationMaxLength = 20000, QuestionOptionsMaxCount = 10, QuestionOptionTextMaxLength = 2000, QuestionBlanksMaxCount = 10, QuestionAcceptedAnswersMaxCount = 20, QuestionAnswerMaxLength = 200, QuestionTagsMaxCount = 10, QuestionTagMaxLength = 50, QuestionMaxScoreMax = 100 };

    [Fact]
    public void Validate_ValidNumeric_ReturnsNoErrors()
    {
        ShortQuestionRules.Validate(Json(Numeric), Json("""{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}"""), _options).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidText_ReturnsNoErrors()
    {
        ShortQuestionRules.Validate(Json(Text), Json("""{"acceptedAnswers":["ماء"]}"""), _options).Should().BeEmpty();
    }

    [Fact]
    public void Validate_MissingAnswerKind_ReturnsQuestionAnswerKindRequired()
    {
        ShortQuestionRules.Validate(Json("{}"), Json("""{"acceptedAnswers":["x"]}"""), _options).Should().Contain(ErrorCodes.QuestionAnswerKindRequired);
    }

    [Fact]
    public void Validate_UnknownAnswerKind_ReturnsQuestionBodyInvalid()
    {
        ShortQuestionRules.Validate(Json("""{"answerKind":"essay"}"""), Json("""{"acceptedAnswers":["x"]}"""), _options).Should().Contain(ErrorCodes.QuestionBodyInvalid);
    }

    [Fact]
    public void Validate_UndefinedAnswerKindNumber_ReturnsQuestionBodyInvalid()
    {
        ShortQuestionRules.Validate(Json("""{"answerKind":5}"""), Json("""{"acceptedAnswers":["x"]}"""), _options).Should().Contain(ErrorCodes.QuestionBodyInvalid);
    }

    [Fact]
    public void Validate_NumericWithoutValue_ReturnsQuestionNumericValueRequired()
    {
        ShortQuestionRules.Validate(Json(Numeric), Json("""{"tolerance":0.1,"toleranceMode":"absolute"}"""), _options).Should().Contain(ErrorCodes.QuestionNumericValueRequired);
    }

    [Fact]
    public void Validate_NegativeTolerance_ReturnsQuestionToleranceInvalid()
    {
        ShortQuestionRules.Validate(Json(Numeric), Json("""{"value":9.8,"tolerance":-1,"toleranceMode":"absolute"}"""), _options).Should().Contain(ErrorCodes.QuestionToleranceInvalid);
    }

    [Fact]
    public void Validate_MissingToleranceMode_ReturnsQuestionToleranceInvalid()
    {
        ShortQuestionRules.Validate(Json(Numeric), Json("""{"value":9.8,"tolerance":0.1}"""), _options).Should().Contain(ErrorCodes.QuestionToleranceInvalid);
    }

    [Fact]
    public void Validate_UndefinedToleranceModeNumber_ReturnsQuestionToleranceInvalid()
    {
        ShortQuestionRules.Validate(Json(Numeric), Json("""{"value":1,"tolerance":0,"toleranceMode":7}"""), _options).Should().Contain(ErrorCodes.QuestionToleranceInvalid);
    }

    [Fact]
    public void Validate_TextWithoutAcceptedAnswers_ReturnsQuestionAcceptedAnswersInvalid()
    {
        ShortQuestionRules.Validate(Json(Text), Json("{}"), _options).Should().Contain(ErrorCodes.QuestionAcceptedAnswersInvalid);
    }

    [Fact]
    public void Normalize_Numeric_KeepsOnlyNumericFields()
    {
        var (_, spec) = ShortQuestionRules.Normalize(Json(Numeric), Json("""{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute","acceptedAnswers":["x"],"unifyLetterVariants":false}"""));

        spec.Should().Be("""{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}""");
    }

    [Fact]
    public void Normalize_Text_TrimsAnswersAndDefaultsAllRulesOn()
    {
        var (_, spec) = ShortQuestionRules.Normalize(Json(Text), Json("""{"acceptedAnswers":[" ماء "],"value":1}"""));

        JsonNode.DeepEquals(JsonNode.Parse(spec), JsonNode.Parse("""{"acceptedAnswers":["ماء"],"normalization":{"stripTashkeel":true,"stripTatweel":true,"unifyAlef":true,"unifyTaaMarbuta":true,"unifyAlefMaqsura":true,"convertDigits":true,"collapseWhitespace":true,"foldCase":true}}""")).Should().BeTrue();
    }

    [Fact]
    public void Normalize_TextWithRulesOff_KeepsChosenRules()
    {
        var (_, spec) = ShortQuestionRules.Normalize(Json(Text), Json("""{"acceptedAnswers":["ماء"],"normalization":{"foldCase":false,"convertDigits":false}}"""));

        JsonNode.DeepEquals(JsonNode.Parse(spec), JsonNode.Parse("""{"acceptedAnswers":["ماء"],"normalization":{"stripTashkeel":true,"stripTatweel":true,"unifyAlef":true,"unifyTaaMarbuta":true,"unifyAlefMaqsura":true,"convertDigits":false,"collapseWhitespace":true,"foldCase":false}}""")).Should().BeTrue();
    }

    [Fact]
    public void Normalize_NumericWithNormalization_DropsNormalization()
    {
        var (_, spec) = ShortQuestionRules.Normalize(Json(Numeric), Json("""{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute","normalization":{"foldCase":false}}"""));

        spec.Should().Be("""{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}""");
    }
}
