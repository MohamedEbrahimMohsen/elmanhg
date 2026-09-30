using Core.DDD.Entities;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;

namespace Elmanhg.Domain.TrainingData;

public class AttemptTrainingRecord : Entity
{
    public string StudentHash { get; private set; } = string.Empty;
    public Guid AttemptId { get; private set; }
    public Guid QuestionId { get; private set; }
    public int QuestionVersion { get; private set; }
    public Guid SubjectId { get; private set; }
    public Guid UnitId { get; private set; }
    public Guid LessonId { get; private set; }
    public SessionKind SessionKind { get; private set; }
    public string Answer { get; private set; } = "{}";
    public decimal Score { get; private set; }
    public decimal NormalisedScore { get; private set; }
    public AttemptGrader GradedBy { get; private set; }
    public string? Grade { get; private set; }
    public int TimeTakenMilliseconds { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    private AttemptTrainingRecord(Guid id) : base(id) { }

    public static AttemptTrainingRecord From(Attempt attempt, SessionKind sessionKind, QuestionPlacement placement, string studentHash, DateTimeOffset recordedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentHash);
        if (placement.QuestionId != attempt.QuestionId)
        {
            throw new InvalidOperationException("Placement does not belong to the attempt's question.");
        }

        return new AttemptTrainingRecord(Guid.NewGuid())
        {
            StudentHash = studentHash,
            AttemptId = attempt.Id,
            QuestionId = attempt.QuestionId,
            QuestionVersion = attempt.QuestionVersion,
            SubjectId = placement.SubjectId,
            UnitId = placement.UnitId,
            LessonId = placement.LessonId,
            SessionKind = sessionKind,
            Answer = attempt.Answer,
            Score = attempt.Score,
            NormalisedScore = attempt.NormalisedScore,
            GradedBy = attempt.GradedBy,
            Grade = attempt.Grade,
            TimeTakenMilliseconds = attempt.TimeTakenMilliseconds,
            OccurredAt = attempt.CreatedAt,
            RecordedAt = recordedAt,
        };
    }
}
