using Elmanhg.Application.Shared.AiService;

namespace Elmanhg.Infrastructure.AiService;

public static class AiEssayGradingReplyRules
{
    public static bool IsValid(AiEssayGradingRequest request, AiEssayGradingResult? result)
    {
        if (result?.Criteria is null || string.IsNullOrWhiteSpace(result.Justification) || string.IsNullOrWhiteSpace(result.Model) || string.IsNullOrWhiteSpace(result.PromptVersion))
        {
            return false;
        }

        if (result.Confidence is < 0m or > 1m || result.CostUsd < 0m || result.InputTokens < 0 || result.OutputTokens < 0)
        {
            return false;
        }

        return HasOneScorePerCriterion(request, result.Criteria)
            && result.TotalPoints == result.Criteria.Sum(x => x.Points)
            && result.MaxPoints == request.Criteria.Sum(x => x.Points);
    }

    private static bool HasOneScorePerCriterion(AiEssayGradingRequest request, IReadOnlyList<AiEssayCriterionScore> scores)
    {
        if (scores.Count != request.Criteria.Count || scores.Any(x => x is null || x.CriterionId is null))
        {
            return false;
        }

        var maxPoints = request.Criteria.ToDictionary(x => x.Id, x => x.Points, StringComparer.Ordinal);
        var ids = scores
            .Select(x => x.CriterionId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        return ids == scores.Count
            && scores.All(x => maxPoints.TryGetValue(x.CriterionId, out var max) && x.Points >= 0 && x.Points <= max && !string.IsNullOrWhiteSpace(x.Justification));
    }
}
