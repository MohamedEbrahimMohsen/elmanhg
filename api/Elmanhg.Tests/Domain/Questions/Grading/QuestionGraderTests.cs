using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using FluentAssertions;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Domain.Questions.Grading;

public sealed class QuestionGraderTests
{
    [Fact]
    public void Grade_McqCorrect_ReturnsFullScoreAndCorrect()
    {
        var grade = QuestionGrader.Grade(QuestionType.Mcq, """{"correctOptionId":"b"}""", 2, Json("""{"optionId":"b"}"""));

        grade.Should().Be(new QuestionGrade(2m, 1m, GradeOutcome.Correct, null));
    }

    [Fact]
    public void Grade_MultiPartial_ReturnsRoundedPartialScore()
    {
        var grade = QuestionGrader.Grade(QuestionType.Multi, """{"correctOptionIds":["a","b","c"],"partialCredit":true}""", 2, Json("""{"optionIds":["a","b","d"]}"""));

        grade.Should().Be(new QuestionGrade(0.67m, 0.3333m, GradeOutcome.Partial, GradeFeedback.ChoiceTally(2, 1, 3)));
    }

    [Fact]
    public void Grade_WrongAnswer_ReturnsZeroAndIncorrect()
    {
        var grade = QuestionGrader.Grade(QuestionType.TrueFalse, """{"correctAnswer":false}""", 1, Json("""{"value":true}"""));

        grade.Score.Should().Be(0m);
        grade.Outcome.Should().Be(GradeOutcome.Incorrect);
    }

    [Fact]
    public void Grade_FillHalf_ReturnsPartialWithBlankTally()
    {
        var grade = QuestionGrader.Grade(QuestionType.Fill, """{"blanks":[{"id":"1","acceptedAnswers":["20"]},{"id":"2","acceptedAnswers":["5"]}],"unifyLetterVariants":true}""", 1, Json("""{"blanks":[{"id":"1","text":"20"}]}"""));

        grade.Should().Be(new QuestionGrade(0.5m, 0.5m, GradeOutcome.Partial, GradeFeedback.BlankTally(1, 2)));
    }

    [Fact]
    public void Grade_ShortPercentJson_ReadsPercentMode()
    {
        var grade = QuestionGrader.Grade(QuestionType.Short, """{"value":200,"tolerance":5,"toleranceMode":"percent"}""", 2, Json("""{"text":"210"}"""));

        grade.Should().Be(new QuestionGrade(2m, 1m, GradeOutcome.Correct, null));
    }

    [Fact]
    public void Grade_ShortNumericWithUnit_ReturnsNotANumberFeedback()
    {
        var grade = QuestionGrader.Grade(QuestionType.Short, """{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}""", 1, Json("""{"text":"9.8 m/s"}"""));

        grade.Should().Be(new QuestionGrade(0m, 0m, GradeOutcome.Incorrect, GradeFeedback.NotANumber));
    }

    [Fact]
    public void Grade_ShortTextUnanswered_ReturnsUnansweredFeedback()
    {
        var grade = QuestionGrader.Grade(QuestionType.Short, """{"acceptedAnswers":["ماء"]}""", 1, Json("{}"));

        grade.Should().Be(new QuestionGrade(0m, 0m, GradeOutcome.Incorrect, GradeFeedback.Unanswered));
    }

    [Fact]
    public void Grade_ShortText_UsesTextGrader()
    {
        var grade = QuestionGrader.Grade(QuestionType.Short, """{"acceptedAnswers":["newton"],"unifyLetterVariants":true}""", 1, Json("""{"text":" Newton "}"""));

        grade.Outcome.Should().Be(GradeOutcome.Correct);
    }

    [Fact]
    public void Grade_FillWithPartialNormalizationJson_AppliesRuleOff()
    {
        var grade = QuestionGrader.Grade(QuestionType.Fill, PartialNormalizationSpec, 1, Json("""{"blanks":[{"id":"1","text":"القاهره"}]}"""));

        grade.Outcome.Should().Be(GradeOutcome.Incorrect);
    }

    [Fact]
    public void Grade_FillWithPartialNormalizationJson_KeepsOtherRulesOn()
    {
        var grade = QuestionGrader.Grade(QuestionType.Fill, PartialNormalizationSpec, 1, Json("""{"blanks":[{"id":"1","text":"القاهرَة"}]}"""));

        grade.Outcome.Should().Be(GradeOutcome.Correct);
    }

    [Fact]
    public void Grade_MultiWithoutPartialCreditKey_DefaultsToAllOrNothing()
    {
        var grade = QuestionGrader.Grade(QuestionType.Multi, """{"correctOptionIds":["a","b"]}""", 1, Json("""{"optionIds":["a"]}"""));

        grade.Should().Be(new QuestionGrade(0m, 0m, GradeOutcome.Incorrect, GradeFeedback.ChoiceTally(1, 0, 2)));
    }

    [Fact]
    public void Grade_MultiTwoThirds_RoundsAwayFromZero()
    {
        var grade = QuestionGrader.Grade(QuestionType.Multi, """{"correctOptionIds":["a","b","c"],"partialCredit":true}""", 1, Json("""{"optionIds":["a","b","c","d"]}"""));

        grade.Should().Be(new QuestionGrade(0.67m, 0.6667m, GradeOutcome.Partial, GradeFeedback.ChoiceTally(3, 1, 3)));
    }

    [Fact]
    public void Grade_McqUnanswered_ReturnsUnansweredFeedback()
    {
        var grade = QuestionGrader.Grade(QuestionType.Mcq, """{"correctOptionId":"b"}""", 2, Json("{}"));

        grade.Should().Be(new QuestionGrade(0m, 0m, GradeOutcome.Incorrect, GradeFeedback.Unanswered));
    }

    [Fact]
    public void Grade_UndefinedType_ThrowsInvalidOperation()
    {
        var act = () => QuestionGrader.Grade((QuestionType)99, """{"correctOptionId":"b"}""", 1, Json("""{"optionId":"b"}"""));

        act.Should().Throw<InvalidOperationException>().WithMessage("Unsupported question type.");
    }

    [Fact]
    public void Grade_NullGradingSpec_ThrowsInvalidOperation()
    {
        var act = () => QuestionGrader.Grade(QuestionType.Mcq, "null", 1, Json("""{"optionId":"b"}"""));

        act.Should().Throw<InvalidOperationException>().WithMessage("Question grading spec is not readable.");
    }

    [Fact]
    public void Grade_NullAnswer_ThrowsInvalidOperation()
    {
        var act = () => QuestionGrader.Grade(QuestionType.Mcq, """{"correctOptionId":"b"}""", 1, Json("null"));

        act.Should().Throw<InvalidOperationException>().WithMessage("Question answer was not validated.");
    }

    [Fact]
    public void Grade_Essay_ThrowsInvalidOperationException()
    {
        var act = () => QuestionGrader.Grade(QuestionType.Essay, EssaySpecJson, 5, Json("{}"));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GradeEssay_PartialAward_ScalesToMaxScore()
    {
        var grade = QuestionGrader.GradeEssay(EssaySpecJson, 5, [new EssayCriterionAward("c1", 1)]);

        grade.Should().Be(new QuestionGrade(2.50m, 0.5m, GradeOutcome.Partial, null));
    }

    [Fact]
    public void GradeMathSteps_Equivalent_ScalesToMaxScore()
    {
        var grade = QuestionGrader.GradeMathSteps(4, MathAnswerVerdict.Equivalent);

        grade.Should().Be(new QuestionGrade(4m, 1m, GradeOutcome.Correct, GradeFeedback.MathFinalAnswerOnly));
    }

    [Fact]
    public void Grade_DragDrop_ScalesPerItemCreditByMaxScore()
    {
        var grade = QuestionGrader.Grade(QuestionType.DragDrop, DragDropSpecJson, 4, Json("""{"placements":[{"zoneId":"z1","itemIds":["i1","i2"]},{"zoneId":"z2","itemIds":["i4"]}]}"""));

        grade.Should().Be(new QuestionGrade(3m, 0.75m, GradeOutcome.Partial, GradeFeedback.PlacementTally(3, 0, 4)));
    }

    [Fact]
    public void Grade_MathSteps_ThrowsInvalidOperationException()
    {
        var act = () => QuestionGrader.Grade(QuestionType.MathSteps, MathStepsSpecJson, 2, Json("{}"));

        act.Should().Throw<InvalidOperationException>();
    }

    private const string PartialNormalizationSpec = """{"blanks":[{"id":"1","acceptedAnswers":["القاهرة"]}],"normalization":{"unifyTaaMarbuta":false}}""";

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Grade_BlankEssay_ReturnsUnanswered(string text)
    {
        var answer = Json($$"""{"text":"{{text}}"}""");

        var grade = QuestionGrader.Grade(QuestionType.Essay, EssaySpecJson, 5, answer);

        grade.Should().Be(new QuestionGrade(0m, 0m, GradeOutcome.Incorrect, GradeFeedback.Unanswered));
    }

    [Fact]
    public void Grade_WrittenEssay_ThrowsInvalidOperationException()
    {
        var act = () => QuestionGrader.Grade(QuestionType.Essay, EssaySpecJson, 5, Json("""{"text":"x"}"""));

        act.Should().Throw<InvalidOperationException>();
    }
}
