namespace Elmanhg.Domain.SharedKernel;

public interface IRetriedWork
{
    bool IsPending { get; }

    void FailAttempt(string errorCode, DateTimeOffset failedAt, int maxAttempts, TimeSpan retryBaseDelay);
}
