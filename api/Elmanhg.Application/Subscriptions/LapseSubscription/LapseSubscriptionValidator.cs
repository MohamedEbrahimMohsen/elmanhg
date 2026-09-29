using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Subscriptions.LapseSubscription;

public sealed class LapseSubscriptionValidator : AbstractValidator<LapseSubscriptionCommand>
{
    public LapseSubscriptionValidator()
    {
        RuleFor(x => x.SubscriptionId).ValidateRequired(ErrorCodes.SubscriptionIdRequired);
    }
}
