using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Questions.Grading;

namespace Elmanhg.Infrastructure.AiService;

public static class AiMathStepGradingReplyRules
{
    public static bool IsValid(AiMathStepGradingRequest request, AiMathStepGradingResult? result)
    {
        if (result?.Steps is null || string.IsNullOrWhiteSpace(result.Justification) || string.IsNullOrWhiteSpace(result.Model) || string.IsNullOrWhiteSpace(result.PromptVersion))
        {
            return false;
        }

        if (result.Confidence is < 0m or > 1m || result.CostUsd < 0m || result.InputTokens < 0 || result.OutputTokens < 0)
        {
            return false;
        }

        return HasOneScorePerModelStep(request.ModelSolution.Count, result.Steps)
            && result.TotalPoints == result.Steps.Sum(x => x.Points)
            && result.MaxPoints == MathStepsGrader.MaxStepPoints * request.ModelSolution.Count;
    }

    private static bool HasOneScorePerModelStep(int modelSteps, IReadOnlyList<AiMathStepScore> scores)
    {
        if (scores.Count != modelSteps || scores.Any(x => x is null))
        {
            return false;
        }

        var indexes = scores
            .Select(x => x.StepIndex)
            .ToHashSet();
        return indexes.SetEquals(Enumerable.Range(0, modelSteps))
            && scores.All(x => x.Points >= 0 && x.Points <= MathStepsGrader.MaxStepPoints && !string.IsNullOrWhiteSpace(x.Justification));
    }
}
