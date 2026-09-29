using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Subscriptions.CancelSubscription;

public sealed class CancelSubscriptionValidator : AbstractValidator<CancelSubscriptionCommand>
{
    public CancelSubscriptionValidator()
    {
        RuleFor(x => x.SubscriptionId).ValidateRequired(ErrorCodes.SubscriptionIdRequired);
    }
}
