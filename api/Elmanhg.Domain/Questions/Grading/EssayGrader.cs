using Elmanhg.Domain.Questions.Schemas;

namespace Elmanhg.Domain.Questions.Grading;

public static class EssayGrader
{
    public static NormalisedGrade Grade(EssayGradingSpec spec, IReadOnlyList<EssayCriterionAward> awards)
    {
        var criteria = spec.Criteria ?? [];
        if (awards.Count != criteria.Count || !criteria.All(criterion => IsAwardedOnce(criterion, awards)))
        {
            throw new InvalidOperationException("Essay awards do not match the rubric.");
        }

        var awarded = awards.Sum(x => x.Points);
        var total = criteria.Sum(x => x.Points!.Value);
        return new NormalisedGrade((decimal)awarded / total, null);
    }

    private static bool IsAwardedOnce(RubricCriterion criterion, IReadOnlyList<EssayCriterionAward> awards)
    {
        var matches = awards
            .Where(x => string.Equals(x.CriterionId, criterion.Id, StringComparison.Ordinal))
            .ToList();
        return matches.Count == 1 && matches[0].Points >= 0 && matches[0].Points <= criterion.Points;
    }
}
