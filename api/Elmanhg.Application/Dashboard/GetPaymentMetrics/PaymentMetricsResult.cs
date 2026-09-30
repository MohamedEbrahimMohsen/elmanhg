using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Domain.SharedKernel;

namespace Elmanhg.Application.Dashboard.GetPaymentMetrics;

public sealed record PaymentMetricsResult(DateOnly From, DateOnly To, int Succeeded, int Failed, int Refunds, Money Revenue, Money Refunded, Money NetRevenue, List<DailyValueResult> RevenueByDay, DateTimeOffset GeneratedAt);
