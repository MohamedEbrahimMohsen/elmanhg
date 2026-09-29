using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.Extensions.Options;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Application.Subscriptions.CancelSubscription;

public sealed class CancelSubscriptionHandler(ISubscriptionRepository subscriptionRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<CancelSubscriptionCommand, EntitlementResult>
{
    public async Task<EntitlementResult> Handle(CancelSubscriptionCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var subscription = await subscriptionRepository.FirstOrDefaultAsync(x => x.Id == request.SubscriptionId && x.StudentId == userId, cancellationToken).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.SubscriptionNotFound);
        var now = timeProvider.GetUtcNow();
        var options = subscriptionsOptions.Value;
        if (subscription.Status != SubscriptionStatus.Cancelled && !subscription.IsEntitledAt(now, options.GracePeriod))
        {
            throw new BusinessRuleViolationCoreException(DomainErrorCodes.SubscriptionEnded);
        }

        subscription.Cancel(now);
        await subscriptionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await StudentEntitlementLoader.LoadAsync(subscriptionRepository, userId, options, now, cancellationToken).ConfigureAwait(false);
    }
}
