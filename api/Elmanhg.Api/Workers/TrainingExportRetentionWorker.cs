using Core.Queues;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TrainingExports.ExpireTrainingExport;
using Elmanhg.Application.TrainingExports.GetExpiredTrainingExportIds;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class TrainingExportRetentionWorker(IServiceScopeFactory scopeFactory, IOptions<TrainingExportsOptions> trainingExportsOptions, TimeProvider timeProvider, ILogger<TrainingExportRetentionWorker> logger, BackgroundJobMetrics jobMetrics) : SweepWorker<TrainingExportsOptions>(scopeFactory, trainingExportsOptions, timeProvider, logger, jobMetrics)
{
    protected override string JobName => "training-export-retention";

    protected override SweepOptions SweepOptionsOf(TrainingExportsOptions options) => new() { Enabled = options.RetentionSweepEnabled, IntervalSeconds = options.RetentionSweepIntervalSeconds, BatchSize = options.RetentionSweepBatchSize };

    protected override async Task<List<Guid>> ListDueAsync(ISender sender, IReadOnlyCollection<Guid> deferredIds, CancellationToken cancellationToken) => await sender.Send(new GetExpiredTrainingExportIdsQuery(deferredIds), cancellationToken).ConfigureAwait(false);

    protected override async Task ProcessAsync(ISender sender, Guid id, CancellationToken cancellationToken) => await sender.Send(new ExpireTrainingExportCommand(id), cancellationToken).ConfigureAwait(false);
}
