using Core.DDD.Repositories;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Settings;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.Extensions.Options;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Application.Subscriptions.CancelSubscription;

public sealed class CancelSubscriptionHandler(ISubscriptionRepository subscriptionRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService, IRuntimeSettings runtimeSettings) : IRequestHandler<CancelSubscriptionCommand, EntitlementResult>
{
    public async Task<EntitlementResult> Handle(CancelSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var subscription = await subscriptionRepository.GetRequiredAsync(x => x.Id == request.SubscriptionId && x.StudentId == userId, ErrorCodes.SubscriptionNotFound, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var options = subscriptionsOptions.Value;
        if (subscription.Status != SubscriptionStatus.Cancelled && !subscription.IsEntitledAt(now, options.GracePeriod))
        {
            throw new BusinessRuleViolationCoreException(DomainErrorCodes.SubscriptionEnded);
        }

        subscription.Cancel(now);
        await subscriptionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await StudentEntitlementLoader.LoadAsync(subscriptionRepository, runtimeSettings, userId, options, now, cancellationToken).ConfigureAwait(false);
    }
}
