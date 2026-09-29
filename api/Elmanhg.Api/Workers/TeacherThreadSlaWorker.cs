using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherThreads.GetDueSlaThreadIds;
using Elmanhg.Application.TeacherThreads.ProcessTeacherThreadSla;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class TeacherThreadSlaWorker(IServiceScopeFactory scopeFactory, IOptions<AskTeacherOptions> askTeacherOptions, TimeProvider timeProvider, ILogger<TeacherThreadSlaWorker> logger, BackgroundJobMetrics jobMetrics) : BackgroundService
{
    private const string JobName = "ask-teacher-sla";

    // Ids whose processing failed are left out of later batches until a sweep reaches the end of the backlog, so failing threads cannot hold the head of every batch.
    private readonly HashSet<Guid> _deferredIds = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = askTeacherOptions.Value;
        if (!options.SlaSweepEnabled)
        {
            return;
        }

        jobMetrics.Register(JobName, TimeSpan.FromSeconds(options.SlaSweepIntervalSeconds));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.SlaSweepIntervalSeconds), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await SweepAsync(options.SlaSweepBatchSize, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task SweepAsync(int batchSize, CancellationToken stoppingToken)
    {
        using var run = jobMetrics.StartRun(JobName);
        var threadIds = await ListDueAsync(stoppingToken).ConfigureAwait(false);
        if (threadIds is null)
        {
            run.MarkListingFailed();
        }

        threadIds ??= [];
        if (threadIds.Count < batchSize)
        {
            _deferredIds.Clear();
        }

        foreach (var threadId in threadIds)
        {
            if (await ProcessAsync(threadId, stoppingToken).ConfigureAwait(false))
            {
                run.ItemSucceeded();
                continue;
            }

            run.ItemFailed();
            _deferredIds.Add(threadId);
        }
    }

    private async Task<List<Guid>?> ListDueAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetDueSlaThreadIdsQuery([.. _deferredIds]), stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Listing due Ask a Teacher SLA threads failed.");
            return null;
        }
    }

    private async Task<bool> ProcessAsync(Guid threadId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessTeacherThreadSlaCommand(threadId), stoppingToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "SLA processing of thread {ThreadId} failed.", threadId);
            return false;
        }
    }
}
