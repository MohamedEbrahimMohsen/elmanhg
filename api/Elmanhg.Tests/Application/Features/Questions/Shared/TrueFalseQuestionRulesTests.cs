using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using FluentAssertions;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Application.Features.Questions.Shared;

public sealed class TrueFalseQuestionRulesTests
{
    [Fact]
    public void Validate_CorrectAnswerFalse_ReturnsNoErrors()
    {
        TrueFalseQuestionRules.Validate(Json("{}"), Json("""{"correctAnswer":false}""")).Should().BeEmpty();
    }

    [Fact]
    public void Validate_MissingCorrectAnswer_ReturnsQuestionCorrectAnswerRequired()
    {
        TrueFalseQuestionRules.Validate(Json("{}"), Json("{}")).Should().Contain(ErrorCodes.QuestionCorrectAnswerRequired);
    }

    [Fact]
    public void Validate_BodyNotAnObject_ReturnsQuestionBodyInvalid()
    {
        TrueFalseQuestionRules.Validate(Json("\"text\""), Json("""{"correctAnswer":true}""")).Should().Contain(ErrorCodes.QuestionBodyInvalid);
    }

    [Fact]
    public void Normalize_AnyBody_StoresEmptyBodyAndCorrectAnswer()
    {
        TrueFalseQuestionRules.Normalize(Json("""{"correctAnswer":true,"note":"x"}""")).Should().Be(("{}", "{\"correctAnswer\":true}"));
    }
}
