using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Subscriptions.GrantComplimentarySubscription;

public sealed class GrantComplimentarySubscriptionValidator : AbstractValidator<GrantComplimentarySubscriptionCommand>
{
    public GrantComplimentarySubscriptionValidator()
    {
        RuleFor(x => x.StudentId).ValidateRequired(ErrorCodes.StudentIdRequired);
        RuleFor(x => x.Plan)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.ComplimentaryPlanInvalid);
        RuleFor(x => x.Period)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.ComplimentaryPeriodInvalid);
    }
}
