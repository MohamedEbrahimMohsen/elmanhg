using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Payments.RefundPayment;

public sealed class RefundPaymentValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentValidator(IOptions<SubscriptionsOptions> subscriptionsOptions)
    {
        var options = subscriptionsOptions.Value;

        RuleFor(x => x.PaymentId).ValidateRequired(ErrorCodes.PaymentIdRequired);
        RuleFor(x => x.Reason)
            .ValidateRequired(ErrorCodes.PaymentRefundReasonRequired)
            .ValidateMaxLength(options.RefundReasonMaxLength, ErrorCodes.PaymentRefundReasonTooLong);
        RuleFor(x => x.IdempotencyKey)
            .ValidateRequired(ErrorCodes.PaymentRefundIdempotencyKeyRequired)
            .Must(x => x != Guid.Empty)
            .WithErrorCode(ErrorCodes.PaymentRefundIdempotencyKeyRequired);
    }
}
