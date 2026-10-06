using Core.Errors;
using Core.Queues;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TrainingExports.FailTrainingExport;
using Elmanhg.Application.TrainingExports.GetDueTrainingExportIds;
using Elmanhg.Application.TrainingExports.RunTrainingExport;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class TrainingExportWorker(IServiceScopeFactory scopeFactory, IOptions<TrainingExportsOptions> trainingExportsOptions, TimeProvider timeProvider, ILogger<TrainingExportWorker> logger, BackgroundJobMetrics jobMetrics) : SweepWorker<TrainingExportsOptions>(scopeFactory, trainingExportsOptions, timeProvider, logger, jobMetrics)
{
    protected override string JobName => "training-export";

    protected override SweepOptions SweepOptionsOf(TrainingExportsOptions options) => new() { Enabled = options.SweepEnabled, IntervalSeconds = options.SweepIntervalSeconds, BatchSize = options.SweepBatchSize };

    protected override async Task<List<Guid>> ListDueAsync(ISender sender, IReadOnlyCollection<Guid> deferredIds, CancellationToken cancellationToken) => await sender.Send(new GetDueTrainingExportIdsQuery(), cancellationToken).ConfigureAwait(false);

    protected override async Task ProcessAsync(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new RunTrainingExportCommand(id), cancellationToken).ConfigureAwait(false);
        }
        catch (ConflictCoreException exception) when (exception.ErrorCode == ErrorCodes.TrainingExportModifiedConcurrently && !cancellationToken.IsCancellationRequested)
        {
            Logger.LogInformation("Training export {TrainingExportId} was claimed by another run.", id);
        }
    }

    protected override async Task RecordFailureAsync(ISender sender, Guid id, string errorCode, CancellationToken cancellationToken) => await sender.Send(new FailTrainingExportCommand(id, errorCode), cancellationToken).ConfigureAwait(false);
}
