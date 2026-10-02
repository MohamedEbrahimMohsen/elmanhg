using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Subscriptions.Shared;

public sealed class PlanCatalogueResultGeneratorTests
{
    [Fact]
    public void Generate_UsesPlanLimitsAndReplySla()
    {
        var options = new SubscriptionsOptions { AskTeacherMonthlyPriceMinor = 9900, BasePrices = { [BillingPeriod.Monthly] = new PlanPriceOptions { Months = 1, AmountMinor = 19900 } } };
        var limits = new PlanLimits(FreeDailyQuizQuestions: 3, FreeDailyAvatarMessages: 4, FreeOpenLessonsPerUnit: 2, BaseDailyAvatarMessages: 77, AskTeacherMonthlyQuestions: 9);

        var result = PlanCatalogueResultGenerator.Generate(options, limits, 36);

        result.Free.Should().Be(new FreePlanResult(3, 4, 2));
        result.Base.DailyAvatarMessages.Should().Be(77);
        (result.AskTeacher.MonthlyQuestions, result.AskTeacher.ReplySlaHours).Should().Be((9, 36));
        (result.Base.Prices.Single().Price.AmountMinor, result.AskTeacher.Prices.Single().Price.AmountMinor).Should().Be((19900L, 9900L));
    }
}
