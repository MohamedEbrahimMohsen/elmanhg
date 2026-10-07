using Core.DDD.Entities;
using Core.DDD.Models;
using Core.DDD.Time;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.SharedKernel;
using System.Text.Json;

namespace Elmanhg.Domain.MathStepGrading;

public partial class MathStepGrade : AuditEntity, IRetriedWork, IVersioned
{
    public Guid StudentId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid QuestionId { get; private set; }
    public Guid SubjectId { get; private set; }
    public int QuestionVersion { get; private set; }
    public int MaxScore { get; private set; }
    public string Answer { get; private set; } = "{}";
    public MathAnswerVerdict? FinalAnswerVerdict { get; private set; }
    public MathStepGradeStatus Status { get; private set; }
    public MathStepReviewReason? ReviewReason { get; private set; }
    public RetrySchedule Retry { get; private set; } = default!;
    public int Attempts => Retry.Attempts;
    public DateTimeOffset? NextAttemptAt => Retry.NextAttemptAt;
    public string? LastErrorCode => Retry.LastErrorCode;
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? GradedAt { get; private set; }
    public decimal? Score { get; private set; }
    public decimal? NormalisedScore { get; private set; }
    public string? Feedback { get; private set; }
    public string? Steps { get; private set; }
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

    private MathStepGrade(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static MathStepGrade Request(Guid studentId, Guid sessionId, Guid subjectId, Guid questionId, int questionVersion, int maxScore, string answer, MathAnswerVerdict? finalAnswerVerdict, DateTimeOffset requestedAt, int timeTakenMilliseconds)
    {
        var parsed = JsonSerializer.Deserialize<MathStepsAnswer>(answer, QuestionJson.SerializerOptions);
        if (string.IsNullOrWhiteSpace(parsed?.FinalAnswer))
        {
            throw new InvalidOperationException("A blank final answer is graded without the step grader.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(timeTakenMilliseconds);
        var at = requestedAt.TruncateToMicroseconds();
        return new MathStepGrade(Guid.NewGuid(), studentId)
        {
            StudentId = studentId,
            SessionId = sessionId,
            SubjectId = subjectId,
            QuestionId = questionId,
            QuestionVersion = questionVersion,
            MaxScore = maxScore,
            Answer = answer,
            FinalAnswerVerdict = finalAnswerVerdict == MathAnswerVerdict.Unchecked ? null : finalAnswerVerdict,
            Status = MathStepGradeStatus.Pending,
            Retry = RetrySchedule.DueAt(at),
            RequestedAt = at,
            TimeTakenMilliseconds = timeTakenMilliseconds,
        };
    }

    public bool IsDueAt(DateTimeOffset now) => Status == MathStepGradeStatus.Pending && Retry.IsDueAt(now);

    public bool IsPending => Status == MathStepGradeStatus.Pending;

    public bool IsAwaitingApplication => Status == MathStepGradeStatus.Graded && AppliedAt is null;

    public MathStepsAnswer ReadAnswer() => JsonSerializer.Deserialize<MathStepsAnswer>(Answer, QuestionJson.SerializerOptions) ?? new MathStepsAnswer([], string.Empty);

    public IReadOnlyList<MathStepScore> ReadSteps() => Steps is null ? [] : JsonSerializer.Deserialize<List<MathStepScore>>(Steps, QuestionJson.SerializerOptions) ?? [];
}
