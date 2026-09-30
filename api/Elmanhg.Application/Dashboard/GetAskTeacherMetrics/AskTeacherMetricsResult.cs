namespace Elmanhg.Application.Dashboard.GetAskTeacherMetrics;

public sealed record AskTeacherMetricsResult(DateOnly From, DateOnly To, Guid? SubjectId, int OpenThreads, int AwaitingReply, int OverdueNow, int SlaBreaches, int Replies, int RepliedWithinSla, decimal? SlaComplianceRate, long? MedianReplySeconds, DateTimeOffset GeneratedAt);
