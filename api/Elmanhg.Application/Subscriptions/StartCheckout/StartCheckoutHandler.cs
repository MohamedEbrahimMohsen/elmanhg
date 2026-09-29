using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Subscriptions.StartCheckout;

public sealed class StartCheckoutHandler(IPaymentRepository paymentRepository, ISubscriptionRepository subscriptionRepository, IUserRepository userRepository, IPaymentGateway paymentGateway, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<StartCheckoutCommand, CheckoutResult>
{
    public async Task<CheckoutResult> Handle(StartCheckoutCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var studentId = currentUserService.UserId.Value;
        var options = subscriptionsOptions.Value;
        var now = timeProvider.GetUtcNow();
        var plan = request.Plan!.Value;
        var period = request.Period!.Value;

        var subscriptions = await subscriptionRepository.FindAsync(SubscriptionEntitlementSpecification.EntitledFor(studentId, now, options.GracePeriod), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        StudentEntitlement.Resolve(subscriptions, now, options.GracePeriod).EnsureCanPurchase(plan);
        var price = options.PriceFor(plan, period) ?? throw new BadRequestCoreException(ErrorCodes.CheckoutPeriodUnavailable);
        var user = await userRepository.GetByIdAsync(studentId, cancellationToken, asNoTracking: true).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.UserNotFound);

        var payment = Payment.Create(studentId, plan, period, new Money(price.AmountMinor, options.Currency));
        var checkout = await paymentGateway.StartCheckoutAsync(new PaymentCheckoutRequest(payment.Id, payment.Amount, plan, period, new PaymentCustomer(user.DisplayName, user.Email, user.PhoneNumber)), cancellationToken).ConfigureAwait(false);

        await paymentRepository.AddAsync(payment, cancellationToken).ConfigureAwait(false);
        await paymentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CheckoutResult(payment.Id, checkout.RedirectUrl, payment.Amount);
    }
}
