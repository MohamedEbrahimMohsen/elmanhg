using Elmanhg.Application.Exams.AutoSubmitExam;
using Elmanhg.Application.Exams.GetExpiredExamSessionIds;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Shared.Options;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class ExpiredExamSubmissionWorker(IServiceScopeFactory scopeFactory, IOptions<ExamsOptions> examsOptions, TimeProvider timeProvider, ILogger<ExpiredExamSubmissionWorker> logger, BackgroundJobMetrics jobMetrics) : BackgroundService
{
    private const string JobName = "exam-auto-submit";

    // Ids whose auto-submit failed are left out of later batches until a sweep reaches the end of the backlog, so failing exams cannot hold the head of every batch.
    private readonly HashSet<Guid> _deferredIds = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = examsOptions.Value;
        if (!options.AutoSubmitEnabled)
        {
            return;
        }

        jobMetrics.Register(JobName, TimeSpan.FromSeconds(options.AutoSubmitIntervalSeconds));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.AutoSubmitIntervalSeconds), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await SweepAsync(options.AutoSubmitBatchSize, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task SweepAsync(int batchSize, CancellationToken stoppingToken)
    {
        using var run = jobMetrics.StartRun(JobName);
        var sessionIds = await ListExpiredAsync(stoppingToken).ConfigureAwait(false);
        if (sessionIds is null)
        {
            run.MarkListingFailed();
        }

        sessionIds ??= [];
        if (sessionIds.Count < batchSize)
        {
            _deferredIds.Clear();
        }

        foreach (var sessionId in sessionIds)
        {
            if (await SubmitAsync(sessionId, stoppingToken).ConfigureAwait(false))
            {
                run.ItemSucceeded();
                continue;
            }

            run.ItemFailed();
            _deferredIds.Add(sessionId);
        }
    }

    private async Task<List<Guid>?> ListExpiredAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetExpiredExamSessionIdsQuery([.. _deferredIds]), stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Listing expired exams failed.");
            return null;
        }
    }

    private async Task<bool> SubmitAsync(Guid sessionId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new AutoSubmitExamCommand(sessionId), stoppingToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Auto-submit of exam {SessionId} failed.", sessionId);
            return false;
        }
    }
}
