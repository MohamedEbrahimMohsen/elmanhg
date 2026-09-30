using Elmanhg.Domain.SharedKernel;

namespace Elmanhg.Application.Dashboard.GetSubscriberMetrics;

public sealed record SubscriberMetricsResult(DateOnly From, DateOnly To, int ActiveSubscriptions, List<PlanCountResult> ActiveByPlan, int ChurnedInRange, int ChurnedThisMonth, Money MonthlyRecurringRevenue, DateTimeOffset GeneratedAt);
