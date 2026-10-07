using Core.Errors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Queues;

public abstract class SweepWorker<TOptions>(IServiceScopeFactory scopeFactory, IOptions<TOptions> options, TimeProvider timeProvider, ILogger logger, BackgroundJobMetrics jobMetrics) : BackgroundService where TOptions : class
{
    // Ids whose processing failed are left out of later batches until a sweep reaches the end of the backlog, so failing items cannot hold the head of every batch.
    private readonly HashSet<Guid> _deferredIds = [];

    protected abstract string JobName { get; }
    protected IServiceScopeFactory ScopeFactory => scopeFactory;
    protected ILogger Logger => logger;

    protected abstract SweepOptions SweepOptionsOf(TOptions options);
    protected abstract Task<List<Guid>> ListDueAsync(ISender sender, IReadOnlyCollection<Guid> deferredIds, CancellationToken cancellationToken);
    protected abstract Task ProcessAsync(ISender sender, Guid id, CancellationToken cancellationToken);
    protected virtual Task RecordFailureAsync(ISender sender, Guid id, string errorCode, CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task BeforeListAsync(SweepOptions sweep, CancellationToken cancellationToken) => Task.CompletedTask;

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sweep = SweepOptionsOf(options.Value);
        if (!sweep.Enabled)
        {
            return;
        }

        jobMetrics.Register(JobName, sweep.Interval);
        using var timer = new PeriodicTimer(sweep.Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await SweepAsync(sweep, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task SweepAsync(SweepOptions sweep, CancellationToken stoppingToken)
    {
        using var run = jobMetrics.StartRun(JobName);
        await TryBeforeListAsync(sweep, stoppingToken).ConfigureAwait(false);
        var ids = await TryListDueAsync(stoppingToken).ConfigureAwait(false);
        if (ids is null)
        {
            run.MarkListingFailed();
        }

        ids ??= [];
        if (ids.Count < sweep.BatchSize)
        {
            _deferredIds.Clear();
        }

        foreach (var id in ids)
        {
            if (await TryProcessAsync(id, stoppingToken).ConfigureAwait(false))
            {
                run.ItemSucceeded();
                continue;
            }

            run.ItemFailed();
            _deferredIds.Add(id);
        }
    }

    private async Task TryBeforeListAsync(SweepOptions sweep, CancellationToken stoppingToken)
    {
        try
        {
            await BeforeListAsync(sweep, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "The pre-listing step of job {JobName} failed.", JobName);
        }
    }

    private async Task<List<Guid>?> TryListDueAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            return await ListDueAsync(scope.ServiceProvider.GetRequiredService<ISender>(), [.. _deferredIds], stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Listing due items of job {JobName} failed.", JobName);
            return null;
        }
    }

    private async Task<bool> TryProcessAsync(Guid id, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await ProcessAsync(scope.ServiceProvider.GetRequiredService<ISender>(), id, stoppingToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Job {JobName} failed on item {ItemId}.", JobName, id);
            await TryRecordFailureAsync(id, BaseException.ErrorCodeOf(exception), stoppingToken).ConfigureAwait(false);
            return false;
        }
    }

    private async Task TryRecordFailureAsync(Guid id, string errorCode, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await RecordFailureAsync(scope.ServiceProvider.GetRequiredService<ISender>(), id, errorCode, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Recording the failure of item {ItemId} in job {JobName} failed.", id, JobName);
        }
    }
}
