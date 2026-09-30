using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Schemas;
using FluentAssertions;
using System.Text.Json;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Application.Features.Questions.Shared;

public sealed class MathStepsAnswerRulesTests
{
    private readonly SessionsOptions _options = new();

    [Fact]
    public void CanRead_ValidShape_ReturnsTrue()
    {
        QuestionAnswerRules.CanRead(QuestionType.MathSteps, Json("""{"steps":["a"],"finalAnswer":"x"}""")).Should().BeTrue();
    }

    [Fact]
    public void CanRead_MissingFields_ReturnsTrue()
    {
        QuestionAnswerRules.CanRead(QuestionType.MathSteps, Json("{}")).Should().BeTrue();
    }

    [Fact]
    public void CanRead_NumberFinalAnswer_ReturnsFalse()
    {
        QuestionAnswerRules.CanRead(QuestionType.MathSteps, Json("""{"finalAnswer":5}""")).Should().BeFalse();
    }

    [Fact]
    public void CanRead_NullStep_ReturnsFalse()
    {
        QuestionAnswerRules.CanRead(QuestionType.MathSteps, Json("""{"steps":[null]}""")).Should().BeFalse();
    }

    [Fact]
    public void Canonicalize_TrimsAndDropsBlankSteps()
    {
        var canonical = QuestionAnswerRules.Canonicalize(QuestionType.MathSteps, Json("""{"steps":[" 2x = 4 ","  "],"finalAnswer":" x = 2 "}"""));

        QuestionJson.AreEquivalent(canonical, """{"steps":["2x = 4"],"finalAnswer":"x = 2"}""").Should().BeTrue();
    }

    [Fact]
    public void ExceedsLimits_TooManySteps_ReturnsTrue()
    {
        QuestionAnswerRules.ExceedsLimits(QuestionType.MathSteps, Answer(Enumerable.Repeat("x", 21), "x"), _options).Should().BeTrue();
    }

    [Fact]
    public void ExceedsLimits_StepTooLong_ReturnsTrue()
    {
        QuestionAnswerRules.ExceedsLimits(QuestionType.MathSteps, Answer([new string('x', 501)], "x"), _options).Should().BeTrue();
    }

    [Fact]
    public void ExceedsLimits_FinalAnswerTooLong_ReturnsTrue()
    {
        QuestionAnswerRules.ExceedsLimits(QuestionType.MathSteps, Answer([], new string('x', 201)), _options).Should().BeTrue();
    }

    [Fact]
    public void ExceedsLimits_WithinLimitsOrOtherType_ReturnsFalse()
    {
        QuestionAnswerRules.ExceedsLimits(QuestionType.MathSteps, Answer(Enumerable.Repeat(new string('x', 500), 20), new string('x', 200)), _options).Should().BeFalse();
        QuestionAnswerRules.ExceedsLimits(QuestionType.Short, Json($$"""{"text":"{{new string('x', 5000)}}"}"""), _options).Should().BeFalse();
    }

    private static JsonElement Answer(IEnumerable<string> steps, string finalAnswer) => Json(JsonSerializer.Serialize(new { steps, finalAnswer }));
}
