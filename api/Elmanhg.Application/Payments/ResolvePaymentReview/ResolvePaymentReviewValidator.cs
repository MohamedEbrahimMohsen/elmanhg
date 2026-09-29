using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Payments.ResolvePaymentReview;

public sealed class ResolvePaymentReviewValidator : AbstractValidator<ResolvePaymentReviewCommand>
{
    public ResolvePaymentReviewValidator()
    {
        RuleFor(x => x.PaymentId).ValidateRequired(ErrorCodes.PaymentIdRequired);
    }
}
