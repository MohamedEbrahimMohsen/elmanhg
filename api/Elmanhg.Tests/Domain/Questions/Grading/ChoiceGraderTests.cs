using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions.Grading;

public sealed class ChoiceGraderTests
{
    [Fact]
    public void GradeMcq_CorrectOption_ReturnsOne()
    {
        ChoiceGrader.GradeMcq(new McqGradingSpec("b"), new McqAnswer("b")).Should().Be(1m);
    }

    [Theory]
    [InlineData("a")]
    [InlineData(null)]
    public void GradeMcq_WrongOrMissingOption_ReturnsZero(string? optionId)
    {
        ChoiceGrader.GradeMcq(new McqGradingSpec("b"), new McqAnswer(optionId)).Should().Be(0m);
    }

    [Fact]
    public void GradeTrueFalse_MatchingValue_ReturnsOne()
    {
        ChoiceGrader.GradeTrueFalse(new TrueFalseGradingSpec(false), new TrueFalseAnswer(false)).Should().Be(1m);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public void GradeTrueFalse_WrongOrMissingValue_ReturnsZero(bool? value)
    {
        ChoiceGrader.GradeTrueFalse(new TrueFalseGradingSpec(false), new TrueFalseAnswer(value)).Should().Be(0m);
    }

    [Fact]
    public void GradeMulti_ExactSetWithoutPartialCredit_ReturnsOne()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "c"]), new MultiAnswer(["c", "a"])).Should().Be(1m);
    }

    [Fact]
    public void GradeMulti_SubsetWithoutPartialCredit_ReturnsZero()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "c"]), new MultiAnswer(["a"])).Should().Be(0m);
    }

    [Fact]
    public void GradeMulti_PartialCredit_ReturnsRightMinusWrongOverTotal()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "b", "c"], PartialCredit: true), new MultiAnswer(["a", "b", "d"])).Should().Be(1m / 3m);
    }

    [Fact]
    public void GradeMulti_PartialCreditMoreWrongThanRight_FloorsAtZero()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a", "b", "c"], PartialCredit: true), new MultiAnswer(["a", "d", "e"])).Should().Be(0m);
    }

    [Fact]
    public void GradeMulti_DuplicateSelections_CountOnce()
    {
        ChoiceGrader.GradeMulti(new MultiGradingSpec(["a"]), new MultiAnswer(["a", "a"])).Should().Be(1m);
    }
}
