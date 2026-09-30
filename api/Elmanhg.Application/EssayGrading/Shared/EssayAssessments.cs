using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Questions.Grading;

namespace Elmanhg.Application.EssayGrading.Shared;

public static class EssayAssessments
{
    // Stored as numeric(5,4).
    private const int ConfidenceDecimals = 4;

    public static IReadOnlyList<EssayCriterionAward> Awards(AiEssayGradingResult result)
    {
        return result.Criteria
            .Select(x => new EssayCriterionAward(x.CriterionId, x.Points))
            .ToList();
    }

    public static EssayAssessment From(AiEssayGradingRequest request, AiEssayGradingResult result)
    {
        var criteria = request.Criteria
            .Select(x => Score(x, result))
            .ToList();
        var confidence = Math.Round(result.Confidence, ConfidenceDecimals, MidpointRounding.AwayFromZero);
        return new EssayAssessment(criteria, result.Justification.Trim(), confidence, result.Model, result.PromptVersion, result.InputTokens, result.OutputTokens, result.CostUsd);
    }

    private static EssayCriterionScore Score(AiRubricCriterion criterion, AiEssayGradingResult result)
    {
        var score = result.Criteria.Single(x => x.CriterionId == criterion.Id);
        return new EssayCriterionScore(criterion.Id, criterion.Title, score.Points, criterion.Points, score.Justification.Trim());
    }
}
