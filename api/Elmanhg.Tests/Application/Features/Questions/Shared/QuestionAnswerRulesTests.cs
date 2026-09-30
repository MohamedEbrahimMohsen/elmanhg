using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Schemas;
using FluentAssertions;
using System.Text.Json;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Application.Features.Questions.Shared;

public sealed class QuestionAnswerRulesTests
{
    [Fact]
    public void Canonicalize_McqWithUnknownProperty_DropsIt()
    {
        var canonical = QuestionAnswerRules.Canonicalize(QuestionType.Mcq, Json("""{"optionId":"b","x":1}"""));

        canonical.Should().Be("""{"optionId":"b"}""");
    }

    [Theory]
    [InlineData(QuestionType.Multi, """{ "optionIds" : ["a","c"], "extra": true }""", """{"optionIds":["a","c"]}""")]
    [InlineData(QuestionType.TrueFalse, """{ "value" : false }""", """{"value":false}""")]
    [InlineData(QuestionType.Fill, """{"blanks":[{"id":"1","text":"20","note":"x"}]}""", """{"blanks":[{"id":"1","text":"20"}]}""")]
    [InlineData(QuestionType.Short, """{ "text" : "Newton", "draft": 2 }""", """{"text":"Newton"}""")]
    public void Canonicalize_EachType_RoundTripsTypedAnswer(QuestionType type, string answer, string expected)
    {
        var canonical = QuestionAnswerRules.Canonicalize(type, Json(answer));

        QuestionJson.AreEquivalent(canonical, expected).Should().BeTrue();
    }

    [Fact]
    public void CanRead_EssayWithText_ReturnsTrue()
    {
        QuestionAnswerRules.CanRead(QuestionType.Essay, Json("""{"text":"a"}""")).Should().BeTrue();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"text":5}""")]
    [InlineData("[]")]
    public void CanRead_EssayWithoutText_ReturnsFalse(string answer)
    {
        QuestionAnswerRules.CanRead(QuestionType.Essay, Json(answer)).Should().BeFalse();
    }

    [Fact]
    public void Canonicalize_Essay_TrimsTextAndDropsUnknownProperties()
    {
        var canonical = QuestionAnswerRules.Canonicalize(QuestionType.Essay, Json("""{"text":"  a b ","x":1}"""));

        canonical.Should().Be("""{"text":"a b"}""");
    }

    [Fact]
    public void TryReadWrittenEssay_WrittenEssay_ReturnsTrimmedText()
    {
        var read = QuestionAnswerRules.TryReadWrittenEssay(QuestionType.Essay, Json("""{"text":"  القصور الذاتي  "}"""), out var text);

        (read, text).Should().Be((true, "القصور الذاتي"));
    }

    [Theory]
    [InlineData(QuestionType.Essay, """{"text":"   "}""")]
    [InlineData(QuestionType.Short, """{"text":"Newton"}""")]
    public void TryReadWrittenEssay_BlankOrOtherType_ReturnsFalse(QuestionType type, string answer)
    {
        var read = QuestionAnswerRules.TryReadWrittenEssay(type, Json(answer), out var text);

        (read, text).Should().Be((false, (string?)null));
    }

    [Fact]
    public void IsEssayTooLong_OverMax_ReturnsTrue()
    {
        var over = QuestionAnswerRules.IsEssayTooLong(QuestionType.Essay, Json("""{"text":"abcdef"}"""), 5);
        var atMax = QuestionAnswerRules.IsEssayTooLong(QuestionType.Essay, Json("""{"text":"abcde"}"""), 5);

        (over, atMax).Should().Be((true, false));
    }

    [Fact]
    public void IsRawAnswerTooLong_EssayUsesEssayCapAndOtherTypesKeepAnswerCap()
    {
        var options = new SessionsOptions { AnswerMaxLength = 20, EssayAnswerMaxLength = 40 };
        var answer = Json("""{"text":"thirty characters long"}""");

        var essay = QuestionAnswerRules.IsRawAnswerTooLong(QuestionType.Essay, answer, options);
        var shortAnswer = QuestionAnswerRules.IsRawAnswerTooLong(QuestionType.Short, answer, options);

        (essay, shortAnswer).Should().Be((false, true));
    }

    [Fact]
    public void IsRawAnswerTooLong_MathStepsUsesMathStepsCap()
    {
        var options = new SessionsOptions { AnswerMaxLength = 20, MathStepsAnswerMaxLength = 40 };
        var answer = Json("""{"steps":["2x=4"],"finalAnswer":"x=2"}""");

        var mathSteps = QuestionAnswerRules.IsRawAnswerTooLong(QuestionType.MathSteps, answer, options);
        var shortAnswer = QuestionAnswerRules.IsRawAnswerTooLong(QuestionType.Short, answer, options);

        (mathSteps, shortAnswer).Should().Be((false, true));
    }

    [Theory]
    [InlineData(4001, false)]
    [InlineData(23999, false)]
    [InlineData(24001, true)]
    public void IsRawAnswerTooLong_MathStepsAtConfiguredCaps_ComparesAgainstMathStepsCap(int rawLength, bool expected)
    {
        var options = new SessionsOptions { AnswerMaxLength = 4000, MathStepsAnswerMaxLength = 24000 };
        var answer = MathStepsAnswerOfRawLength(rawLength);

        QuestionAnswerRules.IsRawAnswerTooLong(QuestionType.MathSteps, answer, options).Should().Be(expected);
    }

    [Fact]
    public void IsRawAnswerTooLong_DragDropUsesDragDropCap()
    {
        var options = new SessionsOptions { AnswerMaxLength = 4000, DragDropAnswerMaxLength = 50 };
        var answer = Json("""{"placements":[{"zoneId":"z1","itemIds":["i1","i2","i30"]}]}""");
        answer.GetRawText().Length.Should().Be(60);

        var dragDrop = QuestionAnswerRules.IsRawAnswerTooLong(QuestionType.DragDrop, answer, options);
        var mcq = QuestionAnswerRules.IsRawAnswerTooLong(QuestionType.Mcq, answer, options);

        (dragDrop, mcq).Should().Be((true, false));
    }

    [Fact]
    public void ExceedsLimits_DragDrop_AppliesPlacementCaps()
    {
        var options = new SessionsOptions { DragDropPlacementsMaxCount = 1 };
        var answer = Json("""{"placements":[{"zoneId":"z1","itemIds":["i1"]},{"zoneId":"z2","itemIds":["i4"]}]}""");

        var dragDrop = QuestionAnswerRules.ExceedsLimits(QuestionType.DragDrop, answer, options);
        var mcq = QuestionAnswerRules.ExceedsLimits(QuestionType.Mcq, answer, options);

        (dragDrop, mcq).Should().Be((true, false));
    }

    private static JsonElement MathStepsAnswerOfRawLength(int rawLength)
    {
        const string Prefix = "{\"steps\":[\"";
        const string Suffix = "\"],\"finalAnswer\":\"x=2\"}";
        var answer = Json(Prefix + new string('a', rawLength - Prefix.Length - Suffix.Length) + Suffix);
        answer.GetRawText().Length.Should().Be(rawLength);
        return answer;
    }
}
