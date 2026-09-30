using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using System.Text.Json.Nodes;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Application.Features.Questions.Shared;

public sealed class MathStepsQuestionRulesTests
{
    private readonly ContentOptions _options = new() { SubjectNameMaxLength = 100, UnitNameMaxLength = 100, LessonNameMaxLength = 100, LessonExplanationMaxLength = 100000, LessonSummaryMaxLength = 20000, LessonObjectiveMaxLength = 300, LessonObjectivesMaxCount = 20, LessonVideoUrlMaxLength = 2048, LessonImageMaxSizeInMb = 5, QuestionStemMaxLength = 20000, QuestionExplanationMaxLength = 20000, QuestionOptionsMaxCount = 10, QuestionOptionTextMaxLength = 2000, QuestionBlanksMaxCount = 10, QuestionAcceptedAnswersMaxCount = 20, QuestionAnswerMaxLength = 200, QuestionTagsMaxCount = 10, QuestionTagMaxLength = 50, QuestionMaxScoreMax = 100 };

    [Fact]
    public void Validate_ValidSpec_ReturnsNoErrors()
    {
        Validate(MathStepsSpecJson).Should().BeEmpty();
    }

    [Fact]
    public void Validate_BodyNotObject_ReturnsQuestionBodyInvalid()
    {
        MathStepsQuestionRules.Validate(Json("[]"), Json(MathStepsSpecJson), _options).Should().Equal(ErrorCodes.QuestionBodyInvalid);
    }

    [Fact]
    public void Validate_SpecNotObject_ReturnsQuestionGradingSpecInvalid()
    {
        Validate("\"x\"").Should().Equal(ErrorCodes.QuestionGradingSpecInvalid);
    }

    [Fact]
    public void Validate_NoAcceptedAnswers_ReturnsQuestionMathAnswersInvalid()
    {
        Validate("""{"acceptedAnswers":[]}""").Should().Equal(ErrorCodes.QuestionMathAnswersInvalid);
    }

    [Fact]
    public void Validate_BlankAcceptedAnswer_ReturnsQuestionMathAnswersInvalid()
    {
        Validate("""{"acceptedAnswers":["  "]}""").Should().Equal(ErrorCodes.QuestionMathAnswersInvalid);
    }

    [Fact]
    public void Validate_AcceptedAnswerTooLong_ReturnsQuestionMathAnswersInvalid()
    {
        _options.QuestionAnswerMaxLength = 3;

        Validate("""{"acceptedAnswers":["x = 2"]}""").Should().Equal(ErrorCodes.QuestionMathAnswersInvalid);
    }

    [Fact]
    public void Validate_TooManyAcceptedAnswers_ReturnsQuestionMathAnswersInvalid()
    {
        _options.QuestionAcceptedAnswersMaxCount = 1;

        Validate("""{"acceptedAnswers":["x = 2","2"]}""").Should().Equal(ErrorCodes.QuestionMathAnswersInvalid);
    }

    [Fact]
    public void Validate_UndefinedFormNumber_ReturnsQuestionMathFormInvalid()
    {
        Validate("""{"acceptedAnswers":["2"],"form":7}""").Should().Equal(ErrorCodes.QuestionMathFormInvalid);
    }

    [Fact]
    public void Validate_UnknownFormName_ReturnsQuestionGradingSpecInvalid()
    {
        Validate("""{"acceptedAnswers":["2"],"form":"fancy"}""").Should().Equal(ErrorCodes.QuestionGradingSpecInvalid);
    }

    [Fact]
    public void Validate_NegativeTolerance_ReturnsQuestionMathToleranceInvalid()
    {
        Validate("""{"acceptedAnswers":["2"],"tolerance":-0.1,"toleranceMode":"absolute"}""").Should().Equal(ErrorCodes.QuestionMathToleranceInvalid);
    }

    [Fact]
    public void Validate_ToleranceWithoutMode_ReturnsQuestionMathToleranceInvalid()
    {
        Validate("""{"acceptedAnswers":["2"],"tolerance":0.1}""").Should().Equal(ErrorCodes.QuestionMathToleranceInvalid);
    }

    [Fact]
    public void Validate_ModeWithoutTolerance_ReturnsQuestionMathToleranceInvalid()
    {
        Validate("""{"acceptedAnswers":["2"],"toleranceMode":"percent"}""").Should().Equal(ErrorCodes.QuestionMathToleranceInvalid);
    }

    [Fact]
    public void Validate_ToleranceWithFactoredForm_ReturnsQuestionMathToleranceFormConflict()
    {
        var errors = Validate("""{"acceptedAnswers":["2"],"form":"factored","tolerance":0.1,"toleranceMode":"absolute"}""");

        errors.Should().Contain(ErrorCodes.QuestionMathToleranceFormConflict).And.NotContain(ErrorCodes.QuestionMathToleranceInvalid);
    }

    [Fact]
    public void Validate_ToleranceWithoutForm_ReturnsNoErrors()
    {
        Validate("""{"acceptedAnswers":["2"],"tolerance":1,"toleranceMode":"percent"}""").Should().BeEmpty();
    }

    [Fact]
    public void Normalize_Spec_TrimsAnswersDefaultsFormAndDropsUnknownFields()
    {
        var (body, spec) = MathStepsQuestionRules.Normalize(Json("""{"x":1}"""), Json("""{"acceptedAnswers":["  x = 2 "],"extra":1}"""));

        body.Should().Be("{}");
        JsonNode.DeepEquals(JsonNode.Parse(spec), JsonNode.Parse("""{"acceptedAnswers":["x = 2"],"form":"equivalent"}""")).Should().BeTrue();
    }

    [Fact]
    public void Normalize_WithTolerance_KeepsToleranceAndMode()
    {
        var (_, spec) = MathStepsQuestionRules.Normalize(Json("{}"), Json("""{"acceptedAnswers":["2"],"tolerance":0.01,"toleranceMode":"absolute"}"""));

        JsonNode.DeepEquals(JsonNode.Parse(spec), JsonNode.Parse("""{"acceptedAnswers":["2"],"form":"equivalent","tolerance":0.01,"toleranceMode":"absolute"}""")).Should().BeTrue();
    }

    private List<string> Validate(string spec) => MathStepsQuestionRules.Validate(Json("{}"), Json(spec), _options);
}
