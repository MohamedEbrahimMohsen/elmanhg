namespace Elmanhg.Domain.TeacherThreads;

public sealed record TeacherReplyStats
{
    public int Replies { get; init; }
    public int RepliedWithinSla { get; init; }
    public double? MedianReplySeconds { get; init; }
}
