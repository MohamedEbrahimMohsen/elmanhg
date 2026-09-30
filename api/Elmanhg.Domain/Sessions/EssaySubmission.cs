namespace Elmanhg.Domain.Sessions;

public sealed record EssaySubmission(bool IsNew, int TimeTakenMilliseconds, DateTimeOffset SubmittedAt)
{
    public static EssaySubmission Replay { get; } = new(false, 0, default);
}
