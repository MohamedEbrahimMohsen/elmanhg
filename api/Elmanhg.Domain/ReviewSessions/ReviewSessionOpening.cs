using Core.DDD.Entities;

namespace Elmanhg.Domain.ReviewSessions;

public class ReviewSessionOpening : Entity
{
    public Guid ReviewSessionId { get; private set; }
    public Guid QuestionId { get; private set; }
    public int QuestionVersion { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }

    private ReviewSessionOpening(Guid id) : base(id) { }

    internal static ReviewSessionOpening Create(Guid reviewSessionId, Guid questionId, int questionVersion)
    {
        return new ReviewSessionOpening(Guid.NewGuid())
        {
            ReviewSessionId = reviewSessionId,
            QuestionId = questionId,
            QuestionVersion = questionVersion,
            OpenedAt = DateTimeOffset.UtcNow,
        };
    }
}
