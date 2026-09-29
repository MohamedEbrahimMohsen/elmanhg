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

namespace Elmanhg.Application.Subscriptions.CompleteFakePayment;

public sealed class CompleteFakePaymentHandler(IPaymentRepository paymentRepository, ISubscriptionRepository subscriptionRepository, IPaymentGateway paymentGateway, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<CompleteFakePaymentCommand, PaymentResult>
{
    private const string FakeTransactionPrefix = "fake-";
    private const string FakeNotificationSource = "fake-gateway";

    public async Task<PaymentResult> Handle(CompleteFakePaymentCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        if (!paymentGateway.SupportsSimulatedCompletion)
        {
            throw new NotFoundCoreException(ErrorCodes.FakeCheckoutUnavailable);
        }

        var userId = currentUserService.UserId.Value;
        var payment = await paymentRepository.FirstOrDefaultAsync(x => x.Id == request.PaymentId && x.StudentId == userId, cancellationToken).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.PaymentNotFound);
        var months = subscriptionsOptions.Value.PriceFor(payment.Plan, payment.Period)?.Months ?? throw new BadRequestCoreException(ErrorCodes.CheckoutPeriodUnavailable);

        var transactionId = FakeTransactionPrefix + payment.Id.ToString("N");
        var rawNotification = JsonSerializer.Serialize(new { source = FakeNotificationSource, success = request.Succeeded });
        var subscription = PaymentSettlement.Settle(payment, request.Succeeded, transactionId, rawNotification, months, timeProvider.GetUtcNow());
        if (subscription is not null)
        {
            await subscriptionRepository.AddAsync(subscription, cancellationToken).ConfigureAwait(false);
        }

        await paymentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return PaymentResultGenerator.Generate(payment);
    }
}
