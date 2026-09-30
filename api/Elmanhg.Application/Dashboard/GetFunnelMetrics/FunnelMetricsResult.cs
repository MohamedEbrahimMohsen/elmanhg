namespace Elmanhg.Application.Dashboard.GetFunnelMetrics;

public sealed record FunnelMetricsResult(DateOnly From, DateOnly To, List<FunnelStepResult> Steps, int CompletedJourneys, long? MedianLandingToFirstAnswerSeconds, DateTimeOffset GeneratedAt);
