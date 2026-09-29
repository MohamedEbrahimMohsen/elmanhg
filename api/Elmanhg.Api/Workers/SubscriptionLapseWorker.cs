using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.GetLapsedSubscriptionIds;
using Elmanhg.Application.Subscriptions.LapseSubscription;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class SubscriptionLapseWorker(IServiceScopeFactory scopeFactory, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ILogger<SubscriptionLapseWorker> logger, BackgroundJobMetrics jobMetrics) : BackgroundService
{
    private const string JobName = "subscription-lapse";

    // Ids whose lapse failed are left out of later batches until a sweep reaches the end of the backlog, so failing subscriptions cannot hold the head of every batch.
    private readonly HashSet<Guid> _deferredIds = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = subscriptionsOptions.Value;
        if (!options.LapseSweepEnabled)
        {
            return;
        }

        jobMetrics.Register(JobName, TimeSpan.FromSeconds(options.LapseSweepIntervalSeconds));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.LapseSweepIntervalSeconds), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await SweepAsync(options.LapseSweepBatchSize, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task SweepAsync(int batchSize, CancellationToken stoppingToken)
    {
        using var run = jobMetrics.StartRun(JobName);
        var subscriptionIds = await ListLapsedAsync(stoppingToken).ConfigureAwait(false);
        if (subscriptionIds is null)
        {
            run.MarkListingFailed();
        }

        subscriptionIds ??= [];
        if (subscriptionIds.Count < batchSize)
        {
            _deferredIds.Clear();
        }

        foreach (var subscriptionId in subscriptionIds)
        {
            if (await LapseAsync(subscriptionId, stoppingToken).ConfigureAwait(false))
            {
                run.ItemSucceeded();
                continue;
            }

            run.ItemFailed();
            _deferredIds.Add(subscriptionId);
        }
    }

    private async Task<List<Guid>?> ListLapsedAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetLapsedSubscriptionIdsQuery([.. _deferredIds]), stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Listing lapsed subscriptions failed.");
            return null;
        }
    }

    private async Task<bool> LapseAsync(Guid subscriptionId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new LapseSubscriptionCommand(subscriptionId), stoppingToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Lapse of subscription {SubscriptionId} failed.", subscriptionId);
            return false;
        }
    }
}
