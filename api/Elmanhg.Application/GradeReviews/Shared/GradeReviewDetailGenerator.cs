using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.MathStepGrading.Shared;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Elmanhg.Application.GradeReviews.Shared;

public static class GradeReviewDetailGenerator
{
    public static GradeReviewDetailResult Essay(EssayGrade grade, QuestionRevisionSnapshot snapshot, GradeReviewPlacement placement)
    {
        var criteria = grade.ReadCriteria()
            .Select(x => new EssayCriterionResult(x.CriterionId, x.Title, x.Points, x.MaxPoints, x.Justification))
            .ToList();
        var finalScore = grade.ReviewDecision is null ? null : grade.FinalScore;
        var review = GradeReviewResultGenerator.Note(grade.ReviewDecision, grade.ReviewComment, grade.ReviewedAt);
        return new GradeReviewDetailResult(grade.Id, nameof(GradeReviewKind.Essay), grade.SubjectId, grade.QuestionId, grade.QuestionVersion, snapshot.Type.ToString(), snapshot.Stem, Element(snapshot.Body), Element(snapshot.GradingSpec), placement.UnitName, placement.LessonName, Answer(grade.Answer), grade.MaxScore, grade.Status.ToString(), grade.ReviewReason!.Value.ToString(), grade.RequestedAt, grade.Score, grade.Confidence, grade.Justification, criteria, [], null, finalScore, review);
    }

    public static GradeReviewDetailResult MathSteps(MathStepGrade grade, QuestionRevisionSnapshot snapshot, GradeReviewPlacement placement)
    {
        var steps = grade.ReadSteps()
            .Select(x => new MathStepScoreResult(x.StepIndex, x.Step, x.Points, x.MaxPoints, x.Justification))
            .ToList();
        var finalScore = grade.ReviewDecision is null ? null : grade.FinalScore;
        var review = GradeReviewResultGenerator.Note(grade.ReviewDecision, grade.ReviewComment, grade.ReviewedAt);
        return new GradeReviewDetailResult(grade.Id, nameof(GradeReviewKind.MathSteps), grade.SubjectId, grade.QuestionId, grade.QuestionVersion, snapshot.Type.ToString(), snapshot.Stem, Element(snapshot.Body), Element(snapshot.GradingSpec), placement.UnitName, placement.LessonName, Answer(grade.Answer), grade.MaxScore, grade.Status.ToString(), grade.ReviewReason!.Value.ToString(), grade.RequestedAt, grade.Score, grade.Confidence, grade.Justification, [], steps, grade.FinalAnswerVerdict?.ToString(), finalScore, review);
    }

    private static JsonElement Element(JsonNode? node) => JsonSerializer.SerializeToElement(node ?? new JsonObject());

    private static JsonElement Answer(string answer)
    {
        using var document = JsonDocument.Parse(answer);
        return document.RootElement.Clone();
    }
}
