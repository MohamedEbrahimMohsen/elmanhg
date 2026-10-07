using Core.Errors;
using Core.Queues;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherThreads.GetDueSlaThreadIds;
using Elmanhg.Application.TeacherThreads.ProcessTeacherThreadSla;
using Elmanhg.Application.TeacherThreads.RescheduleTeacherThreadSlas;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class TeacherThreadSlaWorker(IServiceScopeFactory scopeFactory, IOptions<AskTeacherOptions> askTeacherOptions, TimeProvider timeProvider, ILogger<TeacherThreadSlaWorker> logger, BackgroundJobMetrics jobMetrics) : SweepWorker<AskTeacherOptions>(scopeFactory, askTeacherOptions, timeProvider, logger, jobMetrics)
{
    protected override string JobName => "ask-teacher-sla";

    protected override SweepOptions SweepOptionsOf(AskTeacherOptions options) => new() { Enabled = options.SlaSweepEnabled, IntervalSeconds = options.SlaSweepIntervalSeconds, BatchSize = options.SlaSweepBatchSize };

    protected override async Task BeforeListAsync(SweepOptions sweep, CancellationToken cancellationToken)
    {
        try
        {
            int rescheduled;
            do
            {
                await using var scope = ScopeFactory.CreateAsyncScope();
                rescheduled = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RescheduleTeacherThreadSlasCommand(sweep.BatchSize), cancellationToken).ConfigureAwait(false);
            }
            while (rescheduled == sweep.BatchSize);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.Log(exception is ConflictCoreException ? LogLevel.Warning : LogLevel.Error, exception, "Rescheduling Ask a Teacher SLA deadlines failed.");
        }
    }

    protected override async Task<List<Guid>> ListDueAsync(ISender sender, IReadOnlyCollection<Guid> deferredIds, CancellationToken cancellationToken) => await sender.Send(new GetDueSlaThreadIdsQuery(deferredIds), cancellationToken).ConfigureAwait(false);

    protected override async Task ProcessAsync(ISender sender, Guid id, CancellationToken cancellationToken) => await sender.Send(new ProcessTeacherThreadSlaCommand(id), cancellationToken).ConfigureAwait(false);
}
