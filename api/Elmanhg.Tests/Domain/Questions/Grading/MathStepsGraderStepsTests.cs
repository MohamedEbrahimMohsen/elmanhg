using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions.Grading;

public sealed class MathStepsGraderStepsTests
{
    private static readonly List<string> ThreeSteps = ["2x + 3 = 7", "2x = 4", "x = 2"];

    private static MathStepsGradingSpec Spec(int? weight, List<string>? modelSolution) => new(["x = 2"], MathAnswerForm.Equivalent, null, null, modelSolution, weight);

    private static List<MathStepAward> Awards(params int[] points) => points.Select((value, index) => new MathStepAward(index, value)).ToList();

    [Fact]
    public void Combine_ZeroWeight_ReturnsFinalOnlyGrade()
    {
        MathStepsGrader.Combine(Spec(0, ThreeSteps), MathAnswerVerdict.Equivalent, Awards(0, 0, 0)).Should().Be(MathStepsGrader.Grade(MathAnswerVerdict.Equivalent));
    }

    [Fact]
    public void Combine_NoModelSolution_ReturnsFinalOnlyGrade()
    {
        MathStepsGrader.Combine(Spec(50, null), MathAnswerVerdict.NotEquivalent, null).Should().Be(MathStepsGrader.Grade(MathAnswerVerdict.NotEquivalent));
    }

    [Fact]
    public void Combine_HalfWeightCorrectFinalPartialSteps_ReturnsWeightedValue()
    {
        var grade = MathStepsGrader.Combine(Spec(50, ThreeSteps), MathAnswerVerdict.Equivalent, Awards(2, 1, 0));

        grade.Should().Be(new NormalisedGrade(0.75m, GradeFeedback.MathStepTally(1, 3)));
    }

    [Fact]
    public void Combine_WrongFinalFullSteps_CreditsStepsOnly()
    {
        var grade = MathStepsGrader.Combine(Spec(40, ThreeSteps), MathAnswerVerdict.NotEquivalent, Awards(2, 2, 2));

        grade.Should().Be(new NormalisedGrade(0.4m, GradeFeedback.MathStepTally(3, 3)));
    }

    [Fact]
    public void Combine_NullAwards_TreatsStepsAsZero()
    {
        var grade = MathStepsGrader.Combine(Spec(50, ThreeSteps), MathAnswerVerdict.Equivalent, null);

        grade.Should().Be(new NormalisedGrade(0.5m, GradeFeedback.MathStepTally(0, 3)));
    }

    [Fact]
    public void Combine_AwardsMissingIndex_ThrowsInvalidOperation()
    {
        var act = () => MathStepsGrader.Combine(Spec(50, ThreeSteps), MathAnswerVerdict.Equivalent, Awards(2, 2));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Combine_AwardsDuplicateIndex_ThrowsInvalidOperation()
    {
        List<MathStepAward> awards = [new(0, 2), new(0, 1), new(2, 2)];

        var act = () => MathStepsGrader.Combine(Spec(50, ThreeSteps), MathAnswerVerdict.Equivalent, awards);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Combine_PointsAboveMax_ThrowsInvalidOperation()
    {
        var act = () => MathStepsGrader.Combine(Spec(50, ThreeSteps), MathAnswerVerdict.Equivalent, Awards(3, 0, 0));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Combine_UncheckedVerdict_ThrowsInvalidOperation()
    {
        var act = () => MathStepsGrader.Combine(Spec(50, ThreeSteps), MathAnswerVerdict.Unchecked, Awards(2, 2, 2));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void NeedsStepGrading_WeightModelAndStudentSteps_ReturnsTrue()
    {
        MathStepsGrader.NeedsStepGrading(Spec(50, ThreeSteps), new MathStepsAnswer(["", "2x = 4"], "x = 2")).Should().BeTrue();
    }

    [Fact]
    public void NeedsStepGrading_OnlyBlankStudentSteps_ReturnsFalse()
    {
        MathStepsGrader.NeedsStepGrading(Spec(50, ThreeSteps), new MathStepsAnswer(["  ", null], "x = 2")).Should().BeFalse();
    }

    [Fact]
    public void NeedsStepGrading_ZeroWeight_ReturnsFalse()
    {
        MathStepsGrader.NeedsStepGrading(Spec(0, ThreeSteps), new MathStepsAnswer(["2x = 4"], "x = 2")).Should().BeFalse();
    }

    [Fact]
    public void GradeMathStepsCombined_ScalesToMaxScore()
    {
        const string spec = """{"acceptedAnswers":["x = 2"],"form":"equivalent","modelSolution":["2x + 3 = 7","2x = 4","x = 2"],"stepsWeight":50}""";

        var grade = QuestionGrader.GradeMathStepsCombined(spec, 4, MathAnswerVerdict.Equivalent, Awards(2, 1, 0));

        grade.Should().Be(new QuestionGrade(3m, 0.75m, GradeOutcome.Partial, GradeFeedback.MathStepTally(1, 3)));
    }
}
