using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Subscriptions.Shared;

public sealed class EntitlementResultGeneratorTests
{
    private static readonly DateTimeOffset Now = SubscriptionBuilder.DefaultStart.AddDays(5);
    private static readonly PlanLimits Limits = new(FreeDailyQuizQuestions: 3, FreeDailyAvatarMessages: 4, FreeOpenLessonsPerUnit: 2, BaseDailyAvatarMessages: 77, AskTeacherMonthlyQuestions: 9);
    private readonly SubscriptionsOptions _options = new();
    private readonly Guid _studentId = Guid.Parse("6a1c2e3f-4b5d-4e6f-8a9b-0c1d2e3f4a5b");

    [Fact]
    public void Generate_Free_UsesPlanLimits()
    {
        var entitlement = StudentEntitlement.Resolve([], Now, _options.GracePeriod);

        var result = EntitlementResultGenerator.Generate(entitlement, _options, Limits, Now);

        (result.Tier, result.DailyQuizQuestionLimit, result.DailyAvatarMessageLimit, result.OpenLessonsPerUnit, result.MonthlyAskTeacherQuestionLimit).Should().Be((PlanTier.Free, (int?)3, 4, (int?)2, 0));
    }

    [Fact]
    public void Generate_BaseWithAskTeacher_UsesPlanLimits()
    {
        var subscriptions = new[] { SubscriptionPlan.Base, SubscriptionPlan.AskTeacher }
            .Select(plan => new SubscriptionBuilder().ForStudent(_studentId).WithPlan(plan).InStatus(SubscriptionStatus.Active).Build())
            .ToList();
        var entitlement = StudentEntitlement.Resolve(subscriptions, Now, _options.GracePeriod);

        var result = EntitlementResultGenerator.Generate(entitlement, _options, Limits, Now);

        (result.Tier, result.HasAskTeacher, result.DailyAvatarMessageLimit, result.MonthlyAskTeacherQuestionLimit).Should().Be((PlanTier.Base, true, 77, 9));
    }
}
