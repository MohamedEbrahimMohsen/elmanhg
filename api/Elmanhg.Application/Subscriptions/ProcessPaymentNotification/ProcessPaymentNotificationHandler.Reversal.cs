using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.ProcessPaymentNotification;

public sealed partial class ProcessPaymentNotificationHandler
{
    private async Task<PaymentNotificationResult> ReverseAsync(PaymentNotification notification, CancellationToken cancellationToken)
    {
        if (!notification.Succeeded)
        {
            return new PaymentNotificationResult(null, PaymentNotificationOutcome.Ignored);
        }

        var recorded = await paymentRepository.FirstOrDefaultAsync(x => x.RefundTransactionId == notification.TransactionId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (recorded is not null)
        {
            return new PaymentNotificationResult(recorded.Id, PaymentNotificationOutcome.Duplicate);
        }

        var payment = await FindPaymentAsync(notification, cancellationToken).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.PaymentNotFound);
        if (!IsBound(payment, notification) || notification.AmountMinor <= 0 || notification.AmountMinor > payment.AmountMinor)
        {
            throw new BadRequestCoreException(ErrorCodes.PaymentNotificationMismatch);
        }

        var settledOutcome = payment.Status switch
        {
            PaymentStatus.Refunded => PaymentNotificationOutcome.Duplicate,
            PaymentStatus.Pending => throw new ConflictCoreException(ErrorCodes.PaymentNotSettled),
            PaymentStatus.Failed => PaymentNotificationOutcome.OutOfOrder,
            _ => (PaymentNotificationOutcome?)null,
        };
        if (settledOutcome is { } outcome)
        {
            return new PaymentNotificationResult(payment.Id, outcome);
        }

        var now = timeProvider.GetUtcNow();
        if (notification.AmountMinor < payment.AmountMinor)
        {
            payment.FlagForReview(PaymentReviewReason.PartialRefundAtProvider);
            await paymentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new PaymentNotificationResult(payment.Id, PaymentNotificationOutcome.FlaggedForReview);
        }

        var subscription = payment.SubscriptionId is { } subscriptionId ? await subscriptionRepository.FirstOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken).ConfigureAwait(false) : null;
        PaymentRefundSettlement.Apply(payment, subscription, notification.TransactionId, now, null, null, null);
        await paymentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new PaymentNotificationResult(payment.Id, PaymentNotificationOutcome.Refunded);
    }
}
