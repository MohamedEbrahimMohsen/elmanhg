using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.Shared;

public static class PlanCatalogueResultGenerator
{
    public static PlanCatalogueResult Generate(SubscriptionsOptions options, PlanLimits limits, int replySlaHours)
    {
        var free = new FreePlanResult(limits.FreeDailyQuizQuestions, limits.FreeDailyAvatarMessages, limits.FreeOpenLessonsPerUnit);
        var basePrices = options.BasePrices
            .OrderBy(x => x.Value.Months)
            .Select(x => new PlanPriceResult(x.Key, x.Value.Months, new Money(x.Value.AmountMinor, options.Currency)))
            .ToList();
        var askTeacher = new AskTeacherPlanResult(limits.AskTeacherMonthlyQuestions, replySlaHours, [new PlanPriceResult(BillingPeriod.Monthly, SubscriptionsOptions.AskTeacherPeriodMonths, new Money(options.AskTeacherMonthlyPriceMinor, options.Currency))]);
        return new PlanCatalogueResult(free, new BasePlanResult(limits.BaseDailyAvatarMessages, basePrices), askTeacher);
    }
}
