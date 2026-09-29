using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Subscriptions.ProcessPaymentNotification;

public sealed partial class ProcessPaymentNotificationHandler(IPaymentRepository paymentRepository, ISubscriptionRepository subscriptionRepository, IPaymentNotificationReader notificationReader, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider) : IRequestHandler<ProcessPaymentNotificationCommand, PaymentNotificationResult>
{
    public async Task<PaymentNotificationResult> Handle(ProcessPaymentNotificationCommand request, CancellationToken cancellationToken)
    {
        var notification = notificationReader.Read(request.Payload, request.Signature);
        if (notification is null || notification.Pending || notification.Kind == PaymentNotificationKind.Other)
        {
            return new PaymentNotificationResult(null, PaymentNotificationOutcome.Ignored);
        }

        if (notification.Kind == PaymentNotificationKind.Reversal)
        {
            return await ReverseAsync(notification, cancellationToken).ConfigureAwait(false);
        }

        var recorded = await paymentRepository.FirstOrDefaultAsync(x => x.PaymobTransactionId == notification.TransactionId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (recorded is not null)
        {
            return new PaymentNotificationResult(recorded.Id, PaymentNotificationOutcome.Duplicate);
        }

        var payment = await FindPaymentAsync(notification, cancellationToken).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.PaymentNotFound);
        if (!IsBound(payment, notification) || payment.AmountMinor != notification.AmountMinor)
        {
            throw new BadRequestCoreException(ErrorCodes.PaymentNotificationMismatch);
        }

        var now = timeProvider.GetUtcNow();
        if (!notification.Succeeded)
        {
            if (payment.Status != PaymentStatus.Pending)
            {
                return new PaymentNotificationResult(payment.Id, PaymentNotificationOutcome.OutOfOrder);
            }

            PaymentSettlement.Fail(payment, notification.TransactionId, request.Payload, now);
            await paymentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new PaymentNotificationResult(payment.Id, PaymentNotificationOutcome.MarkedFailed);
        }

        if (payment.Status is PaymentStatus.Succeeded or PaymentStatus.Refunded)
        {
            return new PaymentNotificationResult(payment.Id, PaymentNotificationOutcome.OutOfOrder);
        }

        var options = subscriptionsOptions.Value;
        var subscriptions = await subscriptionRepository.FindAsync(SubscriptionEntitlementSpecification.EntitledFor(payment.StudentId, now, options.GracePeriod), cancellationToken).ConfigureAwait(false);
        var entitlement = StudentEntitlement.Resolve(subscriptions, now, options.GracePeriod);
        var started = PaymentSettlement.Succeed(payment, entitlement, notification.TransactionId, request.Payload, options.GracePeriod, now);
        if (started is not null)
        {
            await subscriptionRepository.AddAsync(started, cancellationToken).ConfigureAwait(false);
        }

        await paymentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new PaymentNotificationResult(payment.Id, PaymentNotificationOutcome.Succeeded);
    }

    private static bool IsBound(Payment payment, PaymentNotification notification) => payment.Currency == notification.Currency && (payment.ProviderOrderId is null || payment.ProviderOrderId == notification.ProviderOrderId);

    private async Task<Payment?> FindPaymentAsync(PaymentNotification notification, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(notification.MerchantOrderId, out var paymentId))
        {
            return await paymentRepository.FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken).ConfigureAwait(false);
        }

        return notification.ProviderOrderId is null ? null : await paymentRepository.FirstOrDefaultAsync(x => x.ProviderOrderId == notification.ProviderOrderId, cancellationToken).ConfigureAwait(false);
    }
}
