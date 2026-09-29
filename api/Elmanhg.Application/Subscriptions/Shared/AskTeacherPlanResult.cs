namespace Elmanhg.Application.Subscriptions.Shared;

public sealed record AskTeacherPlanResult(int MonthlyQuestions, int ReplySlaHours, List<PlanPriceResult> Prices);
