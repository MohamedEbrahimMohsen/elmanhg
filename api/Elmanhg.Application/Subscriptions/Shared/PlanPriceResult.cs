using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.Shared;

public sealed record PlanPriceResult(BillingPeriod Period, int Months, Money Price);
