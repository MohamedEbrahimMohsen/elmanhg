using Core.Queues;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.GetLapsedSubscriptionIds;
using Elmanhg.Application.Subscriptions.LapseSubscription;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class SubscriptionLapseWorker(IServiceScopeFactory scopeFactory, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ILogger<SubscriptionLapseWorker> logger, BackgroundJobMetrics jobMetrics) : SweepWorker<SubscriptionsOptions>(scopeFactory, subscriptionsOptions, timeProvider, logger, jobMetrics)
{
    protected override string JobName => "subscription-lapse";

    protected override SweepOptions SweepOptionsOf(SubscriptionsOptions options) => new() { Enabled = options.LapseSweepEnabled, IntervalSeconds = options.LapseSweepIntervalSeconds, BatchSize = options.LapseSweepBatchSize };

    protected override async Task<List<Guid>> ListDueAsync(ISender sender, IReadOnlyCollection<Guid> deferredIds, CancellationToken cancellationToken) => await sender.Send(new GetLapsedSubscriptionIdsQuery(deferredIds), cancellationToken).ConfigureAwait(false);

    protected override async Task ProcessAsync(ISender sender, Guid id, CancellationToken cancellationToken) => await sender.Send(new LapseSubscriptionCommand(id), cancellationToken).ConfigureAwait(false);
}
