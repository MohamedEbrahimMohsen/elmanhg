using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Questions.Grading;

namespace Elmanhg.Application.EssayGrading.Shared;

public static class EssayGradeResultGenerator
{
    public static EssayGradeResult Generate(EssayGrade grade)
    {
        if (grade.IsAwaitingApplication)
        {
            return new EssayGradeResult(grade.Id, nameof(EssayGradeStatus.Pending), grade.MaxScore, grade.RequestedAt, null, null, null, null, null, []);
        }

        if (grade.Status != EssayGradeStatus.Graded)
        {
            return new EssayGradeResult(grade.Id, grade.Status.ToString(), grade.MaxScore, grade.RequestedAt, null, null, null, null, null, []);
        }

        var criteria = grade.ReadCriteria()
            .Select(Criterion)
            .ToList();
        return new EssayGradeResult(grade.Id, grade.Status.ToString(), grade.MaxScore, grade.RequestedAt, grade.GradedAt, grade.Score, grade.NormalisedScore, QuestionGrade.ToOutcome(grade.NormalisedScore!.Value).ToString(), grade.Justification, criteria);
    }

    public static EssayGradeDetailResult Detail(EssayAssessment assessment)
    {
        var criteria = assessment.Criteria
            .Select(Criterion)
            .ToList();
        return new EssayGradeDetailResult(criteria, assessment.Justification, assessment.Confidence, assessment.Model, assessment.PromptVersion, assessment.CostUsd);
    }

    private static EssayCriterionResult Criterion(EssayCriterionScore x) => new(x.CriterionId, x.Title, x.Points, x.MaxPoints, x.Justification);
}
