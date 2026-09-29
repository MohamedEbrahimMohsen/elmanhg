using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.StartCheckout;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Subscriptions.StartCheckout;

public sealed class StartCheckoutValidatorTests
{
    private readonly SubscriptionsOptions _options = new()
    {
        AskTeacherMonthlyPriceMinor = 9900,
        BasePrices =
        {
            [BillingPeriod.Monthly] = new() { Months = 1, AmountMinor = 19900 },
            [BillingPeriod.Termly] = new() { Months = 4, AmountMinor = 69900 },
            [BillingPeriod.Yearly] = new() { Months = 12, AmountMinor = 179900 },
        },
    };

    [Fact]
    public void Validate_BaseMonthly_Passes()
    {
        var result = Validate(new StartCheckoutCommand(SubscriptionPlan.Base, BillingPeriod.Monthly));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PlanMissing_FailsWithPlanRequired()
    {
        var result = Validate(new StartCheckoutCommand(null, BillingPeriod.Monthly));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.CheckoutPlanRequired);
    }

    [Fact]
    public void Validate_PlanOutOfRange_FailsWithPlanInvalid()
    {
        var result = Validate(new StartCheckoutCommand((SubscriptionPlan)99, BillingPeriod.Monthly));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.CheckoutPlanInvalid).And.NotContain(ErrorCodes.CheckoutPeriodUnavailable);
    }

    [Fact]
    public void Validate_PeriodMissing_FailsWithPeriodRequired()
    {
        var result = Validate(new StartCheckoutCommand(SubscriptionPlan.Base, null));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.CheckoutPeriodRequired);
    }

    [Fact]
    public void Validate_PeriodOutOfRange_FailsWithPeriodInvalid()
    {
        var result = Validate(new StartCheckoutCommand(SubscriptionPlan.Base, (BillingPeriod)99));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.CheckoutPeriodInvalid);
    }

    [Fact]
    public void Validate_AskTeacherYearly_FailsWithPeriodUnavailable()
    {
        var result = Validate(new StartCheckoutCommand(SubscriptionPlan.AskTeacher, BillingPeriod.Yearly));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.CheckoutPeriodUnavailable);
    }

    [Fact]
    public void Validate_BasePeriodNotConfigured_FailsWithPeriodUnavailable()
    {
        _options.BasePrices.Remove(BillingPeriod.Termly);
        _options.BasePrices.Remove(BillingPeriod.Yearly);

        var result = Validate(new StartCheckoutCommand(SubscriptionPlan.Base, BillingPeriod.Yearly));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.CheckoutPeriodUnavailable);
    }

    private FluentValidation.Results.ValidationResult Validate(StartCheckoutCommand command) => new StartCheckoutValidator(Options.Create(_options)).Validate(command);
}
