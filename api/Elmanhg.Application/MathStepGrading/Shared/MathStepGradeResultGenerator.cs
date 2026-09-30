using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions.Grading;

namespace Elmanhg.Application.MathStepGrading.Shared;

public static class MathStepGradeResultGenerator
{
    public static MathStepGradeResult Generate(MathStepGrade grade)
    {
        if (grade.IsAwaitingApplication)
        {
            return new MathStepGradeResult(grade.Id, nameof(MathStepGradeStatus.Pending), grade.MaxScore, grade.RequestedAt, null, null, null, null, null, null, [], null);
        }

        if (grade.Status != MathStepGradeStatus.Graded)
        {
            return new MathStepGradeResult(grade.Id, grade.Status.ToString(), grade.MaxScore, grade.RequestedAt, null, null, null, null, null, null, [], null);
        }

        var overridden = grade.ReviewDecision == GradeReviewDecision.Overridden;
        List<MathStepScoreResult> steps = overridden
            ? []
            : grade.ReadSteps()
                .Select(Step)
                .ToList();
        var review = GradeReviewResultGenerator.Note(grade.ReviewDecision, grade.ReviewComment, grade.ReviewedAt);
        return new MathStepGradeResult(grade.Id, grade.Status.ToString(), grade.MaxScore, grade.RequestedAt, grade.GradedAt, grade.FinalScore, grade.FinalNormalisedScore, QuestionGrade.ToOutcome(grade.FinalNormalisedScore!.Value).ToString(), grade.FinalAnswerVerdict?.ToString(), overridden ? null : grade.Justification, steps, review);
    }

    public static MathStepGradeDetailResult Detail(MathStepAssessment assessment, MathAnswerVerdict verdict)
    {
        var steps = assessment.Steps
            .Select(Step)
            .ToList();
        return new MathStepGradeDetailResult(steps, verdict.ToString(), assessment.Justification, assessment.Confidence, assessment.Model, assessment.PromptVersion, assessment.CostUsd);
    }

    private static MathStepScoreResult Step(MathStepScore x) => new(x.StepIndex, x.Step, x.Points, x.MaxPoints, x.Justification);
}
