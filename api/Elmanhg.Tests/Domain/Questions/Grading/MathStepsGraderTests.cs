using Elmanhg.Domain.Questions.Grading;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions.Grading;

public sealed class MathStepsGraderTests
{
    [Fact]
    public void Grade_NullVerdict_ReturnsUnanswered()
    {
        MathStepsGrader.Grade(null).Should().Be(NormalisedGrade.Unanswered);
    }

    [Fact]
    public void Grade_Equivalent_ReturnsFullWithFinalOnlyFeedback()
    {
        MathStepsGrader.Grade(MathAnswerVerdict.Equivalent).Should().Be(new NormalisedGrade(1m, GradeFeedback.MathFinalAnswerOnly));
    }

    [Fact]
    public void Grade_NotEquivalent_ReturnsZeroWithFinalOnlyFeedback()
    {
        MathStepsGrader.Grade(MathAnswerVerdict.NotEquivalent).Should().Be(new NormalisedGrade(0m, GradeFeedback.MathFinalAnswerOnly));
    }

    [Fact]
    public void Grade_WrongForm_ReturnsZeroWithWrongFormFeedback()
    {
        MathStepsGrader.Grade(MathAnswerVerdict.WrongForm).Should().Be(new NormalisedGrade(0m, GradeFeedback.MathWrongForm));
    }

    [Fact]
    public void Grade_Unreadable_ReturnsZeroWithUnreadableFeedback()
    {
        MathStepsGrader.Grade(MathAnswerVerdict.Unreadable).Should().Be(new NormalisedGrade(0m, GradeFeedback.MathUnreadable));
    }

    [Fact]
    public void Grade_Unchecked_ReturnsZeroWithUncheckedFeedback()
    {
        MathStepsGrader.Grade(MathAnswerVerdict.Unchecked).Should().Be(new NormalisedGrade(0m, GradeFeedback.MathUnchecked));
    }
}
