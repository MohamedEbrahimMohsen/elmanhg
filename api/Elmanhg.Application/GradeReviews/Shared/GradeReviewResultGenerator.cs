using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;

namespace Elmanhg.Application.GradeReviews.Shared;

public static class GradeReviewResultGenerator
{
    public static GradeReviewNoteResult? Note(GradeReviewDecision? decision, string? comment, DateTimeOffset? reviewedAt)
    {
        return decision is null || reviewedAt is null ? null : new GradeReviewNoteResult(decision.Value.ToString(), comment, reviewedAt.Value);
    }

    public static GradeReviewItemResult Item(EssayGrade grade, Question? question, GradeReviewPlacement placement)
    {
        return new GradeReviewItemResult(grade.Id, nameof(GradeReviewKind.Essay), grade.QuestionId, question?.Stem ?? string.Empty, placement.UnitName, placement.LessonName, grade.ReviewReason!.Value.ToString(), grade.MaxScore, grade.Score, grade.Confidence, grade.RequestedAt);
    }

    public static GradeReviewItemResult Item(MathStepGrade grade, Question? question, GradeReviewPlacement placement)
    {
        return new GradeReviewItemResult(grade.Id, nameof(GradeReviewKind.MathSteps), grade.QuestionId, question?.Stem ?? string.Empty, placement.UnitName, placement.LessonName, grade.ReviewReason!.Value.ToString(), grade.MaxScore, grade.Score, grade.Confidence, grade.RequestedAt);
    }
}
