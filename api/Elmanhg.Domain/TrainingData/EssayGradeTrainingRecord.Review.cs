using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;

namespace Elmanhg.Domain.TrainingData;

public partial class EssayGradeTrainingRecord
{
    public static EssayGradeTrainingRecord FromReview(EssayGrade grade, SessionKind sessionKind, QuestionPlacement placement, string studentHash, DateTimeOffset recordedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentHash);
        if (placement.QuestionId != grade.QuestionId)
        {
            throw new InvalidOperationException("Placement does not belong to the grade's question.");
        }

        if (grade is not { ReviewDecision: { } decision, ReviewedAt: { } reviewedAt, ReviewedScore: { } reviewedScore, ReviewedNormalisedScore: { } reviewedNormalised, GradedAt: { } gradedAt, Score: { } score, NormalisedScore: { } normalisedScore, Criteria: { } criteria, Justification: { } justification, Confidence: { } confidence, Model: { } model, PromptVersion: { } promptVersion })
        {
            throw new InvalidOperationException("Only a reviewed AI grade becomes a review training record.");
        }

        return new EssayGradeTrainingRecord(Guid.NewGuid())
        {
            StudentHash = studentHash,
            EssayGradeId = grade.Id,
            QuestionId = grade.QuestionId,
            QuestionVersion = grade.QuestionVersion,
            SubjectId = placement.SubjectId,
            UnitId = placement.UnitId,
            LessonId = placement.LessonId,
            SessionKind = sessionKind,
            Answer = grade.Answer,
            MaxScore = grade.MaxScore,
            Score = score,
            NormalisedScore = normalisedScore,
            Criteria = criteria,
            Justification = justification,
            Confidence = confidence,
            Outcome = EssayGradeStatus.InReview,
            Model = model,
            PromptVersion = promptVersion,
            OccurredAt = gradedAt,
            RecordedAt = recordedAt,
            Trigger = EssayGradeTrainingTrigger.TeacherReviewed,
            ReviewDecision = decision,
            ReviewedScore = reviewedScore,
            ReviewedNormalisedScore = reviewedNormalised,
            ReviewComment = grade.ReviewComment,
            ReviewedAt = reviewedAt,
        };
    }
}
