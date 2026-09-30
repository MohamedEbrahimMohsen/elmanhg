using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TrainingExports.FailTrainingExport;
using Elmanhg.Application.TrainingExports.GetDueTrainingExportIds;
using Elmanhg.Application.TrainingExports.RunTrainingExport;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class TrainingExportWorker(IServiceScopeFactory scopeFactory, IOptions<TrainingExportsOptions> trainingExportsOptions, TimeProvider timeProvider, ILogger<TrainingExportWorker> logger, BackgroundJobMetrics jobMetrics) : BackgroundService
{
    private const string JobName = "training-export";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = trainingExportsOptions.Value;
        if (!options.SweepEnabled)
        {
            return;
        }

        jobMetrics.Register(JobName, TimeSpan.FromSeconds(options.SweepIntervalSeconds));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.SweepIntervalSeconds), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await SweepAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task SweepAsync(CancellationToken stoppingToken)
    {
        using var run = jobMetrics.StartRun(JobName);
        var exportIds = await ListDueAsync(stoppingToken).ConfigureAwait(false);
        if (exportIds is null)
        {
            run.MarkListingFailed();
            return;
        }

        foreach (var exportId in exportIds)
        {
            if (await RunAsync(exportId, stoppingToken).ConfigureAwait(false))
            {
                run.ItemSucceeded();
                continue;
            }

            run.ItemFailed();
        }
    }

    private async Task<List<Guid>?> ListDueAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetDueTrainingExportIdsQuery(), stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Listing due training exports failed.");
            return null;
        }
    }

    private async Task<bool> RunAsync(Guid exportId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunTrainingExportCommand(exportId), stoppingToken).ConfigureAwait(false);
            return true;
        }
        catch (ConflictCoreException exception) when (exception.ErrorCode == ErrorCodes.TrainingExportModifiedConcurrently && !stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Training export {TrainingExportId} was claimed by another run.", exportId);
            return true;
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Training export {TrainingExportId} failed.", exportId);
            await RecordFailureAsync(exportId, ErrorCodeOf(exception), stoppingToken).ConfigureAwait(false);
            return false;
        }
    }

    private async Task RecordFailureAsync(Guid exportId, string errorCode, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new FailTrainingExportCommand(exportId, errorCode), stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Recording the failed training export {TrainingExportId} failed.", exportId);
        }
    }

    private static string ErrorCodeOf(Exception exception) => exception is BaseException { ErrorCode: { Length: > 0 } code } ? code : exception.GetType().Name;
}
