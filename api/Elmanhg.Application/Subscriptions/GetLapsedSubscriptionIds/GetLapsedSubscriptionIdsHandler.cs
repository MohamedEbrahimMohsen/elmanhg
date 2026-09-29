using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Subscriptions.GetLapsedSubscriptionIds;

public sealed class GetLapsedSubscriptionIdsHandler(ISubscriptionRepository subscriptionRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider) : IRequestHandler<GetLapsedSubscriptionIdsQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetLapsedSubscriptionIdsQuery request, CancellationToken cancellationToken)
    {
        var options = subscriptionsOptions.Value;
        var page = await subscriptionRepository.FindPaginatedAsync(1, options.LapseSweepBatchSize, cancellationToken, filter: SubscriptionLapseSpecification.DueAt(timeProvider.GetUtcNow(), options.GracePeriod, request.ExcludedIds), orderBy: query => query.OrderBy(x => x.CurrentPeriodEnd), asNoTracking: true).ConfigureAwait(false);
        return page.Items
            .Select(x => x.Id)
            .ToList();
    }
}
