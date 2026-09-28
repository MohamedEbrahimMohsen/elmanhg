using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions.Grading;

public sealed class ChoiceGraderTests
{
    [Fact]
    public void GradeMcq_CorrectOption_ReturnsOne()
    {
        ChoiceGrader.GradeMcq(new McqGradingSpec("b"), new McqAnswer("b")).Value.Should().Be(1m);
    }

    [Theory]
    [InlineData("a")]
    [InlineData(null)]
    public void GradeMcq_WrongOrMissingOption_ReturnsZero(string? optionId)
    {
        ChoiceGrader.GradeMcq(new McqGradingSpec("b"), new McqAnswer(optionId)).Value.Should().Be(0m);
    }

    [Fact]
    public void GradeTrueFalse_MatchingValue_ReturnsOne()
    {
        ChoiceGrader.GradeTrueFalse(new TrueFalseGradingSpec(false), new TrueFalseAnswer(false)).Value.Should().Be(1m);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public void GradeTrueFalse_WrongOrMissingValue_ReturnsZero(bool? value)
    {
        ChoiceGrader.GradeTrueFalse(new TrueFalseGradingSpec(false), new TrueFalseAnswer(value)).Value.Should().Be(0m);
    }

    [Fact]
    public void GradeMulti_ExactSetWithoutPartialCredit_ReturnsOne()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "c"]), new MultiAnswer(["c", "a"])).Value.Should().Be(1m);
    }

    [Fact]
    public void GradeMulti_SubsetWithoutPartialCredit_ReturnsZero()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "c"]), new MultiAnswer(["a"])).Value.Should().Be(0m);
    }

    [Fact]
    public void GradeMulti_PartialCredit_ReturnsRightMinusWrongOverTotal()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "b", "c"], PartialCredit: true), new MultiAnswer(["a", "b", "d"])).Value.Should().Be(1m / 3m);
    }

    [Fact]
    public void GradeMulti_PartialCreditMoreWrongThanRight_FloorsAtZero()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "b", "c"], PartialCredit: true), new MultiAnswer(["a", "d", "e"])).Value.Should().Be(0m);
    }

    [Fact]
    public void GradeMulti_DuplicateSelections_CountOnce()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a"]), new MultiAnswer(["a", "a"])).Value.Should().Be(1m);
    }

    [Fact]
    public void GradeMcq_CorrectOption_HasNoFeedback()
    {
        ChoiceGrader.GradeMcq(new McqGradingSpec("b"), new McqAnswer("b")).Should().Be(new NormalisedGrade(1m, null));
    }

    [Fact]
    public void GradeMcq_WrongOption_ReturnsZeroWithoutFeedback()
    {
        ChoiceGrader.GradeMcq(new McqGradingSpec("b"), new McqAnswer("a")).Should().Be(new NormalisedGrade(0m, null));
    }

    [Fact]
    public void GradeMcq_NoOption_ReturnsUnanswered()
    {
        ChoiceGrader.GradeMcq(new McqGradingSpec("b"), new McqAnswer(null)).Should().Be(NormalisedGrade.Unanswered);
    }

    [Fact]
    public void GradeTrueFalse_TrueMatchesTrue_ReturnsOne()
    {
        ChoiceGrader.GradeTrueFalse(new TrueFalseGradingSpec(true), new TrueFalseAnswer(true)).Should().Be(new NormalisedGrade(1m, null));
    }

    [Fact]
    public void GradeTrueFalse_WrongValue_ReturnsZeroWithoutFeedback()
    {
        ChoiceGrader.GradeTrueFalse(new TrueFalseGradingSpec(false), new TrueFalseAnswer(true)).Should().Be(new NormalisedGrade(0m, null));
    }

    [Fact]
    public void GradeTrueFalse_NoValue_ReturnsUnanswered()
    {
        ChoiceGrader.GradeTrueFalse(new TrueFalseGradingSpec(false), new TrueFalseAnswer(null)).Should().Be(NormalisedGrade.Unanswered);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GradeMulti_ExactSet_HasNoFeedback(bool partialCredit)
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "c"], partialCredit), new MultiAnswer(["c", "a"])).Should().Be(new NormalisedGrade(1m, null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GradeMulti_EmptySelection_ReturnsUnanswered(bool partialCredit)
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "c"], partialCredit), new MultiAnswer([])).Should().Be(NormalisedGrade.Unanswered);
    }

    [Fact]
    public void GradeMulti_NullSelection_ReturnsUnanswered()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "c"]), new MultiAnswer(null)).Should().Be(NormalisedGrade.Unanswered);
    }

    [Fact]
    public void GradeMulti_OnlyNullIds_ReturnsUnanswered()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "c"]), new MultiAnswer([null!])).Should().Be(NormalisedGrade.Unanswered);
    }

    [Fact]
    public void GradeMulti_SubsetWithoutPartialCredit_ReportsTally()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "b"]), new MultiAnswer(["a"])).Should().Be(new NormalisedGrade(0m, GradeFeedback.ChoiceTally(1, 0, 2)));
    }

    [Fact]
    public void GradeMulti_AllCorrectPlusWrongWithoutPartialCredit_ReturnsZero()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "b"]), new MultiAnswer(["a", "b", "c"])).Should().Be(new NormalisedGrade(0m, GradeFeedback.ChoiceTally(2, 1, 2)));
    }

    [Fact]
    public void GradeMulti_AllCorrectPlusWrongWithPartialCredit_ReturnsTwoThirds()
    {
        var grade = ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "b", "c"], PartialCredit: true), new MultiAnswer(["a", "b", "c", "d"]));

        grade.Value.Should().Be(2m / 3m);
        grade.Feedback.Should().Be(GradeFeedback.ChoiceTally(3, 1, 3));
    }

    [Fact]
    public void GradeMulti_PartialCreditRepeatedCorrectId_CountsOnce()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "b"], PartialCredit: true), new MultiAnswer(["a", "a"])).Should().Be(new NormalisedGrade(0.5m, GradeFeedback.ChoiceTally(1, 0, 2)));
    }

    [Fact]
    public void GradeMulti_PartialCreditRepeatedWrongId_CountsOnce()
    {
        var grade = ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "b", "c"], PartialCredit: true), new MultiAnswer(["a", "b", "d", "d"]));

        grade.Value.Should().Be(1m / 3m);
        grade.Feedback.Should().Be(GradeFeedback.ChoiceTally(2, 1, 3));
    }

    [Fact]
    public void GradeMulti_PartialCreditUnknownId_CountsAsWrong()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "b"], PartialCredit: true), new MultiAnswer(["a", "b", "z"])).Should().Be(new NormalisedGrade(0.5m, GradeFeedback.ChoiceTally(2, 1, 2)));
    }

    [Fact]
    public void GradeMulti_PartialCreditEqualRightAndWrong_ReturnsZero()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "b"], PartialCredit: true), new MultiAnswer(["a", "c"])).Should().Be(new NormalisedGrade(0m, GradeFeedback.ChoiceTally(1, 1, 2)));
    }

    [Fact]
    public void GradeMulti_PartialCreditNullIdAmongIds_IsIgnored()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "b"], PartialCredit: true), new MultiAnswer(["a", null!])).Should().Be(new NormalisedGrade(0.5m, GradeFeedback.ChoiceTally(1, 0, 2)));
    }

    [Fact]
    public void GradeMulti_IdsCompareCaseSensitively()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a"], PartialCredit: true), new MultiAnswer(["A"])).Should().Be(new NormalisedGrade(0m, GradeFeedback.ChoiceTally(0, 1, 1)));
    }

    [Fact]
    public void GradeMulti_EmptyCorrectSet_ReturnsZeroWithoutFeedback()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec([], PartialCredit: true), new MultiAnswer(["a"])).Should().Be(new NormalisedGrade(0m, null));
    }
}
