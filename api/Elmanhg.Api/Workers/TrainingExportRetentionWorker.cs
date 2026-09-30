using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TrainingExports.ExpireTrainingExport;
using Elmanhg.Application.TrainingExports.GetExpiredTrainingExportIds;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class TrainingExportRetentionWorker(IServiceScopeFactory scopeFactory, IOptions<TrainingExportsOptions> trainingExportsOptions, TimeProvider timeProvider, ILogger<TrainingExportRetentionWorker> logger, BackgroundJobMetrics jobMetrics) : BackgroundService
{
    private const string JobName = "training-export-retention";

    // Ids whose expiry failed are left out of later batches until a sweep reaches the end of the backlog, so failing exports cannot hold the head of every batch.
    private readonly HashSet<Guid> _deferredIds = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = trainingExportsOptions.Value;
        if (!options.RetentionSweepEnabled)
        {
            return;
        }

        jobMetrics.Register(JobName, TimeSpan.FromSeconds(options.RetentionSweepIntervalSeconds));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.RetentionSweepIntervalSeconds), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await SweepAsync(options.RetentionSweepBatchSize, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task SweepAsync(int batchSize, CancellationToken stoppingToken)
    {
        using var run = jobMetrics.StartRun(JobName);
        var exportIds = await ListExpiredAsync(stoppingToken).ConfigureAwait(false);
        if (exportIds is null)
        {
            run.MarkListingFailed();
        }

        exportIds ??= [];
        if (exportIds.Count < batchSize)
        {
            _deferredIds.Clear();
        }

        foreach (var exportId in exportIds)
        {
            if (await ExpireAsync(exportId, stoppingToken).ConfigureAwait(false))
            {
                run.ItemSucceeded();
                continue;
            }

            run.ItemFailed();
            _deferredIds.Add(exportId);
        }
    }

    private async Task<List<Guid>?> ListExpiredAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetExpiredTrainingExportIdsQuery([.. _deferredIds]), stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Listing expired training exports failed.");
            return null;
        }
    }

    private async Task<bool> ExpireAsync(Guid exportId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ExpireTrainingExportCommand(exportId), stoppingToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Expiry of training export {TrainingExportId} failed.", exportId);
            return false;
        }
    }
}
