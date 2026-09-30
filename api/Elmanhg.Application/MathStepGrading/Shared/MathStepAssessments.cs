using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions.Grading;

namespace Elmanhg.Application.MathStepGrading.Shared;

public static class MathStepAssessments
{
    // Stored as numeric(5,4).
    private const int ConfidenceDecimals = 4;

    public static IReadOnlyList<MathStepAward> Awards(AiMathStepGradingResult result)
    {
        return result.Steps
            .Select(x => new MathStepAward(x.StepIndex, x.Points))
            .ToList();
    }

    public static MathStepAssessment From(AiMathStepGradingRequest request, AiMathStepGradingResult result)
    {
        var steps = request.ModelSolution
            .Select((step, index) => Score(index, step, result))
            .ToList();
        var confidence = Math.Round(result.Confidence, ConfidenceDecimals, MidpointRounding.AwayFromZero);
        return new MathStepAssessment(steps, result.Justification.Trim(), confidence, result.Model, result.PromptVersion, result.InputTokens, result.OutputTokens, result.CostUsd);
    }

    private static MathStepScore Score(int index, string step, AiMathStepGradingResult result)
    {
        var score = result.Steps.Single(x => x.StepIndex == index);
        return new MathStepScore(index, step, score.Points, MathStepsGrader.MaxStepPoints, score.Justification.Trim());
    }
}
