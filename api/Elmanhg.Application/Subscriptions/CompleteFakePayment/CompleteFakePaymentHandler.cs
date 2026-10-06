using Core.DDD.Repositories;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.Extensions.Options;
using System.Text.Json;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Application.Subscriptions.CompleteFakePayment;

public sealed class CompleteFakePaymentHandler(IPaymentRepository paymentRepository, ISubscriptionRepository subscriptionRepository, IPaymentGateway paymentGateway, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<CompleteFakePaymentCommand, PaymentResult>
{
    private const string FakeTransactionPrefix = "fake-";
    private const string FakeNotificationSource = "fake-gateway";

    public async Task<PaymentResult> Handle(CompleteFakePaymentCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        if (!paymentGateway.SupportsSimulatedCompletion)
        {
            throw new NotFoundCoreException(ErrorCodes.FakeCheckoutUnavailable);
        }

        var payment = await paymentRepository.GetRequiredAsync(x => x.Id == request.PaymentId && x.StudentId == userId, ErrorCodes.PaymentNotFound, cancellationToken).ConfigureAwait(false);
        if (payment.Status != PaymentStatus.Pending)
        {
            throw new BusinessRuleViolationCoreException(DomainErrorCodes.PaymentNotPending);
        }

        var transactionId = FakeTransactionPrefix + payment.Id.ToString("N");
        var rawNotification = JsonSerializer.Serialize(new { source = FakeNotificationSource, success = request.Succeeded });
        var now = timeProvider.GetUtcNow();
        if (!request.Succeeded)
        {
            PaymentSettlement.Fail(payment, transactionId, rawNotification, now);
            await paymentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return PaymentResultGenerator.Generate(payment);
        }

        var options = subscriptionsOptions.Value;
        var subscriptions = await subscriptionRepository.FindAsync(SubscriptionEntitlementSpecification.EntitledFor(userId, now, options.GracePeriod), cancellationToken).ConfigureAwait(false);
        var started = PaymentSettlement.Succeed(payment, StudentEntitlement.Resolve(subscriptions, now, options.GracePeriod), transactionId, rawNotification, options.GracePeriod, now);
        if (started is not null)
        {
            await subscriptionRepository.AddAsync(started, cancellationToken).ConfigureAwait(false);
        }

        await paymentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return PaymentResultGenerator.Generate(payment);
    }
}
