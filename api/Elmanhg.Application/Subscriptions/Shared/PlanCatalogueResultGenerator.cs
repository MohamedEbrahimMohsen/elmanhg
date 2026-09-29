using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.Shared;

public static class PlanCatalogueResultGenerator
{
    public static PlanCatalogueResult Generate(SubscriptionsOptions options)
    {
        var free = new FreePlanResult(options.FreeDailyQuizQuestions, options.FreeDailyAvatarMessages, options.FreeOpenLessonsPerUnit);
        var basePrices = options.BasePrices
            .OrderBy(x => x.Value.Months)
            .Select(x => new PlanPriceResult(x.Key, x.Value.Months, new Money(x.Value.AmountMinor, options.Currency)))
            .ToList();
        var askTeacher = new AskTeacherPlanResult(options.AskTeacherMonthlyQuestions, options.AskTeacherReplySlaHours, [new PlanPriceResult(BillingPeriod.Monthly, SubscriptionsOptions.AskTeacherPeriodMonths, new Money(options.AskTeacherMonthlyPriceMinor, options.Currency))]);
        return new PlanCatalogueResult(free, new BasePlanResult(options.BaseDailyAvatarMessages, basePrices), askTeacher);
    }
}
