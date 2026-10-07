using Core.Queues;
using Elmanhg.Application.ContentRetrieval.GetStaleLessonContentIds;
using Elmanhg.Application.ContentRetrieval.ReindexLessonContent;
using Elmanhg.Application.Shared.Options;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class LessonContentIndexWorker(IServiceScopeFactory scopeFactory, IOptions<ContentRetrievalOptions> contentRetrievalOptions, TimeProvider timeProvider, ILogger<LessonContentIndexWorker> logger, BackgroundJobMetrics jobMetrics) : SweepWorker<ContentRetrievalOptions>(scopeFactory, contentRetrievalOptions, timeProvider, logger, jobMetrics)
{
    protected override string JobName => "lesson-content-index";

    protected override SweepOptions SweepOptionsOf(ContentRetrievalOptions options) => new() { Enabled = options.IndexSweepEnabled, IntervalSeconds = options.IndexSweepIntervalSeconds, BatchSize = options.IndexSweepBatchSize };

    protected override async Task<List<Guid>> ListDueAsync(ISender sender, IReadOnlyCollection<Guid> deferredIds, CancellationToken cancellationToken) => await sender.Send(new GetStaleLessonContentIdsQuery(deferredIds), cancellationToken).ConfigureAwait(false);

    protected override async Task ProcessAsync(ISender sender, Guid id, CancellationToken cancellationToken) => await sender.Send(new ReindexLessonContentCommand(id), cancellationToken).ConfigureAwait(false);
}
