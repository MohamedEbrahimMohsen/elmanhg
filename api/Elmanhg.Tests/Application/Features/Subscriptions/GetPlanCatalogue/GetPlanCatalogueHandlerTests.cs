using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.GetPlanCatalogue;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Subscriptions.GetPlanCatalogue;

public sealed class GetPlanCatalogueHandlerTests
{
    private readonly SubscriptionsOptions _options = new()
    {
        BasePrices = new Dictionary<BillingPeriod, PlanPriceOptions>
        {
            [BillingPeriod.Yearly] = new() { Months = 12, AmountMinor = 179900 },
            [BillingPeriod.Monthly] = new() { Months = 1, AmountMinor = 19900 },
            [BillingPeriod.Termly] = new() { Months = 4, AmountMinor = 69900 },
        },
        AskTeacherMonthlyPriceMinor = 9900,
    };

    [Fact]
    public async Task Handle_ConfiguredOptions_ReturnsFreeLimitsAndBasePricesOrderedByMonths()
    {
        var result = await Handle();

        result.Free.Should().Be(new FreePlanResult(10, 5, 1));
        result.Base.DailyAvatarMessages.Should().Be(50);
        result.Base.Prices.Should().Equal(new PlanPriceResult(BillingPeriod.Monthly, 1, new Money(19900, "EGP")), new PlanPriceResult(BillingPeriod.Termly, 4, new Money(69900, "EGP")), new PlanPriceResult(BillingPeriod.Yearly, 12, new Money(179900, "EGP")));
    }

    [Fact]
    public async Task Handle_AskTeacher_ReturnsSingleMonthlyPriceAndQuota()
    {
        var result = await Handle();

        result.AskTeacher.Prices.Should().Equal(new PlanPriceResult(BillingPeriod.Monthly, 1, new Money(9900, "EGP")));
        (result.AskTeacher.MonthlyQuestions, result.AskTeacher.ReplySlaHours).Should().Be((20, 24));
    }

    private Task<PlanCatalogueResult> Handle() => new GetPlanCatalogueHandler(Microsoft.Extensions.Options.Options.Create(_options), new FakeRuntimeSettings(subscriptions: _options)).Handle(new GetPlanCatalogueQuery(), TestContext.Current.CancellationToken);
}
