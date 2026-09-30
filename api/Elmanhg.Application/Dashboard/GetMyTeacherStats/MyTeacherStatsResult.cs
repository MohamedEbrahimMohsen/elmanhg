namespace Elmanhg.Application.Dashboard.GetMyTeacherStats;

public sealed record MyTeacherStatsResult(DateOnly From, DateOnly To, int Approved, int Rejected, long? MedianSecondsToDecision, int Replies, int RepliedWithinSla, decimal? SlaComplianceRate, long? MedianReplySeconds, DateTimeOffset GeneratedAt);
