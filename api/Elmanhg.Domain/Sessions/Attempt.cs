using Core.DDD.Entities;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Domain.Sessions;

public class Attempt : Entity
{
    public Guid SessionId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid QuestionId { get; private set; }
    public int QuestionVersion { get; private set; }
    public string Answer { get; private set; } = "{}";
    public decimal Score { get; private set; }
    public decimal NormalisedScore { get; private set; }
    public AttemptGrader GradedBy { get; private set; }
    public string? Grade { get; private set; }
    public int TimeTakenMilliseconds { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public GradeOutcome Outcome => QuestionGrade.ToOutcome(NormalisedScore);

    private Attempt(Guid id) : base(id) { }

    public GradeFeedback? ReadFeedback() => Grade is null ? null : JsonSerializer.Deserialize<GradeFeedback>(Grade, QuestionJson.SerializerOptions);

    internal static Attempt Create(Session session, SessionItem item, string answer, QuestionGrade grade, int timeTakenMilliseconds, DateTimeOffset createdAt)
    {
        return new Attempt(Guid.NewGuid())
        {
            SessionId = session.Id,
            StudentId = session.StudentId,
            QuestionId = item.QuestionId,
            QuestionVersion = item.QuestionVersion,
            Answer = answer,
            Score = grade.Score,
            NormalisedScore = grade.NormalisedScore,
            GradedBy = AttemptGrader.Auto,
            Grade = grade.Feedback is null ? null : JsonSerializer.Serialize(grade.Feedback, QuestionJson.SerializerOptions),
            TimeTakenMilliseconds = timeTakenMilliseconds,
            CreatedAt = createdAt,
        };
    }
}
