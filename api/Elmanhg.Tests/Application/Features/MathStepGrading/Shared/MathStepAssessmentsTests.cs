using Elmanhg.Application.MathStepGrading.Shared;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions.Grading;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.MathStepGrading.Shared;

public sealed class MathStepAssessmentsTests
{
    private static readonly AiMathStepGradingRequest Request = new("Q", ["2x = 4", "x = 2"], ["x = 2"], ["2x = 4"], "x = 2", null, []);

    [Fact]
    public void From_MapsStepsInModelOrderWithStepText()
    {
        var assessment = MathStepAssessments.From(Request, Reply(0.9m));

        assessment.Steps.Should().Equal(new MathStepScore(0, "2x = 4", 2, 2, "Right."), new MathStepScore(1, "x = 2", 0, 2, "Missing."));
        (assessment.Justification, assessment.Model, assessment.PromptVersion, assessment.InputTokens, assessment.OutputTokens, assessment.CostUsd).Should().Be(("Good.", "claude-sonnet-5", "v1", 900, 150, 0.004m));
    }

    [Fact]
    public void From_RoundsConfidenceToFourDecimals()
    {
        MathStepAssessments.From(Request, Reply(0.12345m)).Confidence.Should().Be(0.1235m);
    }

    [Fact]
    public void Awards_MapsIndexAndPoints()
    {
        MathStepAssessments.Awards(Reply(0.9m)).Should().Equal(new MathStepAward(1, 0), new MathStepAward(0, 2));
    }

    private static AiMathStepGradingResult Reply(decimal confidence) => new([new AiMathStepScore(1, 0, " Missing. "), new AiMathStepScore(0, 2, "Right.")], 2, 4, " Good. ", confidence, "claude-sonnet-5", "v1", 900, 150, "end_turn", 0.004m);
}
