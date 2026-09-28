using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.ReviewSessions;

public class ReviewSession : AuditEntity
{
    public Guid TeacherId { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public List<ReviewSessionOpening> Openings { get; private set; } = [];

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

    private ReviewSession(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static ReviewSession Start(Guid teacherId, TimeSpan lifetime)
    {
        return new ReviewSession(Guid.NewGuid(), teacherId)
        {
            TeacherId = teacherId,
            ExpiresAt = DateTimeOffset.UtcNow.Add(lifetime),
        };
    }

    public bool HasOpened(Question question) => !IsExpired && Openings.Any(x => x.QuestionId == question.Id && x.QuestionVersion == question.Version);

    public void RecordOpening(Question question)
    {
        EnsureActive();
        if (!Openings.Any(x => x.QuestionId == question.Id && x.QuestionVersion == question.Version))
        {
            Openings.Add(ReviewSessionOpening.Create(Id, question.Id, question.Version));
        }

        UpdatedBy = TeacherId;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void EnsureOpened(Question question)
    {
        EnsureActive();
        if (!HasOpened(question))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionNotOpenedInSession, context: new Dictionary<string, object> { ["questionId"] = question.Id });
        }
    }

    private void EnsureActive()
    {
        if (IsExpired)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ReviewSessionExpired);
        }
    }
}
