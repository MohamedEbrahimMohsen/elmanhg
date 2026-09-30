using Elmanhg.Application.Questions.Shared;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Schemas;
using FluentAssertions;
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
}
