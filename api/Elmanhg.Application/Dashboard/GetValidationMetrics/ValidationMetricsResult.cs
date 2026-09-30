using Elmanhg.Application.Dashboard.Shared;

namespace Elmanhg.Application.Dashboard.GetValidationMetrics;

public sealed record ValidationMetricsResult(DateOnly From, DateOnly To, Guid? SubjectId, int PendingBacklog, int Approved, int Rejected, long? MedianSecondsToDecision, List<TeacherThroughputResult> ByTeacher, List<DailyValueResult> DailyDecisions, DateTimeOffset GeneratedAt);
