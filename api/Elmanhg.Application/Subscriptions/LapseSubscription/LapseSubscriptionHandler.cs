using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Subscriptions.LapseSubscription;

public sealed class LapseSubscriptionHandler(ISubscriptionRepository subscriptionRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider) : IRequestHandler<LapseSubscriptionCommand>
{
    public async Task Handle(LapseSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetByIdAsync(request.SubscriptionId, cancellationToken).ConfigureAwait(false);
        if (subscription is null || !subscription.Lapse(timeProvider.GetUtcNow(), subscriptionsOptions.Value.GracePeriod))
        {
            return;
        }

        await subscriptionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
