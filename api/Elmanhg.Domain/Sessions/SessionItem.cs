using Core.DDD.Entities;
using Elmanhg.Domain.Questions;

namespace Elmanhg.Domain.Sessions;

public class SessionItem : Entity
{
    public Guid SessionId { get; private set; }
    public int Position { get; private set; }
    public Guid QuestionId { get; private set; }
    public int QuestionVersion { get; private set; }
    public int MaxScore { get; private set; }
    public string? SavedAnswer { get; private set; }
    public DateTimeOffset? AnswerSavedAt { get; private set; }

    private SessionItem(Guid id) : base(id) { }

    internal static SessionItem Create(Guid sessionId, int position, Question question)
    {
        return new SessionItem(Guid.NewGuid())
        {
            SessionId = sessionId,
            Position = position,
            QuestionId = question.Id,
            QuestionVersion = question.Version,
            MaxScore = question.MaxScore,
        };
    }

    internal void SaveAnswer(string answer, DateTimeOffset savedAt)
    {
        SavedAnswer = answer;
        AnswerSavedAt = savedAt;
    }
}
