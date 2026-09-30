using Core.DDD.Entities;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;

namespace Elmanhg.Domain.TrainingData;

public partial class EssayGradeTrainingRecord : Entity
{
    public string StudentHash { get; private set; } = string.Empty;
    public Guid EssayGradeId { get; private set; }
    public Guid QuestionId { get; private set; }
    public int QuestionVersion { get; private set; }
    public Guid SubjectId { get; private set; }
    public Guid UnitId { get; private set; }
    public Guid LessonId { get; private set; }
    public SessionKind SessionKind { get; private set; }
    public string Answer { get; private set; } = "{}";
    public int MaxScore { get; private set; }
    public decimal Score { get; private set; }
    public decimal NormalisedScore { get; private set; }
    public string Criteria { get; private set; } = "[]";
    public string Justification { get; private set; } = string.Empty;
    public decimal Confidence { get; private set; }
    public EssayGradeStatus Outcome { get; private set; }
    public string Model { get; private set; } = string.Empty;
    public string PromptVersion { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public EssayGradeTrainingTrigger Trigger { get; private set; }
    public GradeReviewDecision? ReviewDecision { get; private set; }
    public decimal? ReviewedScore { get; private set; }
    public decimal? ReviewedNormalisedScore { get; private set; }
    public string? ReviewComment { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    private EssayGradeTrainingRecord(Guid id) : base(id) { }

    public static EssayGradeTrainingRecord From(EssayGrade grade, SessionKind sessionKind, QuestionPlacement placement, string studentHash, DateTimeOffset recordedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentHash);
        if (placement.QuestionId != grade.QuestionId)
        {
            throw new InvalidOperationException("Placement does not belong to the grade's question.");
        }

        if (grade.Status == EssayGradeStatus.Pending || grade is not { GradedAt: { } gradedAt, Score: { } score, NormalisedScore: { } normalisedScore, Criteria: { } criteria, Justification: { } justification, Confidence: { } confidence, Model: { } model, PromptVersion: { } promptVersion })
        {
            throw new InvalidOperationException("Only a completed AI grade becomes a training record.");
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
            Outcome = grade.Status,
            Model = model,
            PromptVersion = promptVersion,
            OccurredAt = gradedAt,
            RecordedAt = recordedAt,
            Trigger = EssayGradeTrainingTrigger.Completed,
        };
    }
}
