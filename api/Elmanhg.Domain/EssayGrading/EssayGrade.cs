using Core.DDD.Entities;
using Core.DDD.Models;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.SharedKernel;
using System.Text.Json;

namespace Elmanhg.Domain.EssayGrading;

public partial class EssayGrade : AuditEntity, IRetriedWork, IVersioned
{
    public Guid StudentId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid QuestionId { get; private set; }
    public Guid SubjectId { get; private set; }
    public int QuestionVersion { get; private set; }
    public int MaxScore { get; private set; }
    public string Answer { get; private set; } = "{}";
    public EssayGradeStatus Status { get; private set; }
    public EssayReviewReason? ReviewReason { get; private set; }
    public RetrySchedule Retry { get; private set; } = default!;
    public int Attempts => Retry.Attempts;
    public DateTimeOffset? NextAttemptAt => Retry.NextAttemptAt;
    public string? LastErrorCode => Retry.LastErrorCode;
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? GradedAt { get; private set; }
    public decimal? Score { get; private set; }
    public decimal? NormalisedScore { get; private set; }
    public string? Criteria { get; private set; }
    public string? Justification { get; private set; }
    public decimal? Confidence { get; private set; }
    public string? Model { get; private set; }
    public string? PromptVersion { get; private set; }
    public int? InputTokens { get; private set; }
    public int? OutputTokens { get; private set; }
    public decimal? CostUsd { get; private set; }
    public int TimeTakenMilliseconds { get; private set; }
    public DateTimeOffset? AppliedAt { get; private set; }
    public uint Version { get; private set; }

    private EssayGrade(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static EssayGrade Request(Guid studentId, Guid sessionId, Guid subjectId, Guid questionId, int questionVersion, int maxScore, string answerText, DateTimeOffset requestedAt, int timeTakenMilliseconds)
    {
        if (string.IsNullOrWhiteSpace(answerText))
        {
            throw new InvalidOperationException("A blank essay is graded without the AI grader.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(timeTakenMilliseconds);
        var at = ToMicroseconds(requestedAt);
        return new EssayGrade(Guid.NewGuid(), studentId)
        {
            StudentId = studentId,
            SessionId = sessionId,
            SubjectId = subjectId,
            QuestionId = questionId,
            QuestionVersion = questionVersion,
            MaxScore = maxScore,
            Answer = JsonSerializer.Serialize(new EssayAnswer(answerText.Trim()), QuestionJson.SerializerOptions),
            Status = EssayGradeStatus.Pending,
            Retry = RetrySchedule.DueAt(at),
            RequestedAt = at,
            TimeTakenMilliseconds = timeTakenMilliseconds,
        };
    }

    public bool IsDueAt(DateTimeOffset now) => Status == EssayGradeStatus.Pending && Retry.IsDueAt(now);

    public bool IsPending => Status == EssayGradeStatus.Pending;

    public bool IsAwaitingApplication => Status == EssayGradeStatus.Graded && AppliedAt is null;

    public string ReadAnswerText() => JsonSerializer.Deserialize<EssayAnswer>(Answer, QuestionJson.SerializerOptions)?.Text ?? string.Empty;

    public IReadOnlyList<EssayCriterionScore> ReadCriteria() => Criteria is null ? [] : JsonSerializer.Deserialize<List<EssayCriterionScore>>(Criteria, QuestionJson.SerializerOptions) ?? [];

    // timestamptz stores whole microseconds; truncating keeps the first response identical to later reads.
    private static DateTimeOffset ToMicroseconds(DateTimeOffset value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMicrosecond));
}
