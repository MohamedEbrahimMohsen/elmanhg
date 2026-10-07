using Core.DDD.Repositories;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Settings;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Payments.Shared;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subscriptions;
using MediatR;

namespace Elmanhg.Application.Payments.RefundPayment;

public sealed class RefundPaymentHandler(IPaymentRepository paymentRepository, ISubscriptionRepository subscriptionRepository, IUserRepository userRepository, IPaymentGateway paymentGateway, ICurrentUserService currentUserService, TimeProvider timeProvider, IRuntimeSettings runtimeSettings) : IRequestHandler<RefundPaymentCommand, AdminPaymentResult>
{
    public async Task<AdminPaymentResult> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        if (!await runtimeSettings.GetAsync(FeatureFlagRuntimeSettings.RefundsEnabled, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentRefundsDisabled);
        }

        var payment = await paymentRepository.GetRequiredAsync(x => x.Id == request.PaymentId, ErrorCodes.PaymentNotFound, cancellationToken).ConfigureAwait(false);
        if (payment.IsRefundReplay(request.IdempotencyKey))
        {
            return await GenerateAsync(payment, cancellationToken).ConfigureAwait(false);
        }

        payment.EnsureRefundable();
        var refund = await paymentGateway.RefundAsync(new PaymentRefundRequest(payment.Id, payment.PaymobTransactionId!, payment.Amount), cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var subscription = payment.SubscriptionId is { } subscriptionId ? await subscriptionRepository.FirstOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken).ConfigureAwait(false) : null;
        PaymentRefundSettlement.Apply(payment, subscription, refund.TransactionId, now, userId, request.Reason!.Trim(), request.IdempotencyKey);
        await paymentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await GenerateAsync(payment, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AdminPaymentResult> GenerateAsync(Payment payment, CancellationToken cancellationToken)
    {
        var student = await userRepository.FirstOrDefaultAsync(x => x.Id == payment.StudentId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return AdminPaymentResultGenerator.Generate(payment, student);
    }
}
