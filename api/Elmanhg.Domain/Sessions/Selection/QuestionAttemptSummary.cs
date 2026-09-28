namespace Elmanhg.Domain.Sessions.Selection;

public sealed record QuestionAttemptSummary(Guid QuestionId, int AttemptCount, int CorrectCount, DateTimeOffset LastAttemptedAt, DateTimeOffset? LastCorrectAt)
{
    // The latest attempt is correct exactly when the latest correct attempt is the latest attempt; this keeps the summary a single GROUP BY.
    public bool IsLastAttemptCorrect => LastCorrectAt == LastAttemptedAt;

    public QuestionSelectionBucket Bucket => !IsLastAttemptCorrect ? QuestionSelectionBucket.LastWrong : CorrectCount == 1 ? QuestionSelectionBucket.CorrectOnce : QuestionSelectionBucket.Rest;
}
