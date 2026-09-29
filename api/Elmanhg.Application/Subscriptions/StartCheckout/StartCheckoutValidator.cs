using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Subscriptions.StartCheckout;

public sealed class StartCheckoutValidator : AbstractValidator<StartCheckoutCommand>
{
    public StartCheckoutValidator(IOptions<SubscriptionsOptions> subscriptionsOptions)
    {
        var options = subscriptionsOptions.Value;

        RuleFor(x => x.Plan)
            .ValidateRequired(ErrorCodes.CheckoutPlanRequired)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.CheckoutPlanInvalid);
        RuleFor(x => x.Period)
            .ValidateRequired(ErrorCodes.CheckoutPeriodRequired)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.CheckoutPeriodInvalid);
        RuleFor(x => x)
            .Must(x => options.PriceFor(x.Plan!.Value, x.Period!.Value) is not null)
            .WithErrorCode(ErrorCodes.CheckoutPeriodUnavailable)
            .When(x => x.Plan.HasValue && x.Period.HasValue && Enum.IsDefined(x.Plan.Value) && Enum.IsDefined(x.Period.Value));
    }
}
