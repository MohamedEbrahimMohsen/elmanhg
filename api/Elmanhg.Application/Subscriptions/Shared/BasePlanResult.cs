namespace Elmanhg.Application.Subscriptions.Shared;

public sealed record BasePlanResult(int DailyAvatarMessages, List<PlanPriceResult> Prices);
