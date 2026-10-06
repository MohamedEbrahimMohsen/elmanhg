namespace Core.DDD.Models;

public sealed class RetrySchedule
{
    // Owners map LastErrorCode to a 100-character column.
    public const int ErrorCodeMaxLength = 100;

    public int Attempts { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? LastErrorCode { get; private set; }

    private RetrySchedule() { }

    public static RetrySchedule DueAt(DateTimeOffset at) => new() { NextAttemptAt = at };

    public bool IsDueAt(DateTimeOffset now) => NextAttemptAt <= now;

    public bool RecordFailure(string? errorCode, DateTimeOffset at, int maxAttempts, TimeSpan baseDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        Attempts++;
        LastErrorCode = errorCode is null || errorCode.Length <= ErrorCodeMaxLength ? errorCode : errorCode[..ErrorCodeMaxLength];
        var exhausted = Attempts >= maxAttempts;
        NextAttemptAt = exhausted ? null : at + (baseDelay * Math.Pow(2, Attempts - 1));
        return exhausted;
    }

    public void RecordSuccess()
    {
        Attempts++;
        NextAttemptAt = null;
        LastErrorCode = null;
    }

    public void Lease(DateTimeOffset at, TimeSpan span) => NextAttemptAt = at + span;
}
