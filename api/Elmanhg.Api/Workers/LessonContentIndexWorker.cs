using Elmanhg.Application.ContentRetrieval.GetStaleLessonContentIds;
using Elmanhg.Application.ContentRetrieval.ReindexLessonContent;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Shared.Options;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class LessonContentIndexWorker(IServiceScopeFactory scopeFactory, IOptions<ContentRetrievalOptions> contentRetrievalOptions, TimeProvider timeProvider, ILogger<LessonContentIndexWorker> logger, BackgroundJobMetrics jobMetrics) : BackgroundService
{
    private const string JobName = "lesson-content-index";

    // Ids whose reindex failed are left out of later batches until a sweep reaches the end of the backlog, so failing lessons cannot hold the head of every batch.
    private readonly HashSet<Guid> _deferredIds = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = contentRetrievalOptions.Value;
        if (!options.IndexSweepEnabled)
        {
            return;
        }

        jobMetrics.Register(JobName, TimeSpan.FromSeconds(options.IndexSweepIntervalSeconds));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.IndexSweepIntervalSeconds), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await SweepAsync(options.IndexSweepBatchSize, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task SweepAsync(int batchSize, CancellationToken stoppingToken)
    {
        using var run = jobMetrics.StartRun(JobName);
        var lessonIds = await ListStaleAsync(stoppingToken).ConfigureAwait(false);
        if (lessonIds is null)
        {
            run.MarkListingFailed();
        }

        lessonIds ??= [];
        if (lessonIds.Count < batchSize)
        {
            _deferredIds.Clear();
        }

        foreach (var lessonId in lessonIds)
        {
            if (await ReindexAsync(lessonId, stoppingToken).ConfigureAwait(false))
            {
                run.ItemSucceeded();
                continue;
            }

            run.ItemFailed();
            _deferredIds.Add(lessonId);
        }
    }

    private async Task<List<Guid>?> ListStaleAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetStaleLessonContentIdsQuery([.. _deferredIds]), stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Listing stale lesson content failed.");
            return null;
        }
    }

    private async Task<bool> ReindexAsync(Guid lessonId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ReindexLessonContentCommand(lessonId), stoppingToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Reindex of lesson {LessonId} failed.", lessonId);
            return false;
        }
    }
}
