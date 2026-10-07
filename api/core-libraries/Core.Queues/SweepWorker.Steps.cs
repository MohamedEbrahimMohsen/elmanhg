using Core.Errors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Queues;

public abstract partial class SweepWorker<TOptions> where TOptions : class
{
    private async Task TryBeforeListAsync(SweepOptions sweep, CancellationToken stoppingToken)
    {
        try
        {
            await BeforeListAsync(sweep, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            Logger.LogError(exception, "The pre-listing step of job {JobName} failed.", JobName);
        }
    }

    private async Task<List<Guid>?> TryListDueAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = ScopeFactory.CreateAsyncScope();
            return await ListDueAsync(scope.ServiceProvider.GetRequiredService<ISender>(), [.. _deferredIds], stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            Logger.LogError(exception, "Listing due items of job {JobName} failed.", JobName);
            return null;
        }
    }

    private async Task<bool> TryProcessAsync(Guid id, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = ScopeFactory.CreateAsyncScope();
            await ProcessAsync(scope.ServiceProvider.GetRequiredService<ISender>(), id, stoppingToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            Logger.LogWarning(exception, "Job {JobName} failed on item {ItemId}.", JobName, id);
            await TryRecordFailureAsync(id, BaseException.ErrorCodeOf(exception), stoppingToken).ConfigureAwait(false);
            return false;
        }
    }

    private async Task TryRecordFailureAsync(Guid id, string errorCode, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = ScopeFactory.CreateAsyncScope();
            await RecordFailureAsync(scope.ServiceProvider.GetRequiredService<ISender>(), id, errorCode, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            Logger.LogError(exception, "Recording the failure of item {ItemId} in job {JobName} failed.", id, JobName);
        }
    }
}
