using Core.Queues;
using Elmanhg.Application.Exams.AutoSubmitExam;
using Elmanhg.Application.Exams.GetExpiredExamSessionIds;
using Elmanhg.Application.Shared.Options;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class ExpiredExamSubmissionWorker(IServiceScopeFactory scopeFactory, IOptions<ExamsOptions> examsOptions, TimeProvider timeProvider, ILogger<ExpiredExamSubmissionWorker> logger, BackgroundJobMetrics jobMetrics) : SweepWorker<ExamsOptions>(scopeFactory, examsOptions, timeProvider, logger, jobMetrics)
{
    protected override string JobName => "exam-auto-submit";

    protected override SweepOptions SweepOptionsOf(ExamsOptions options) => new() { Enabled = options.AutoSubmitEnabled, IntervalSeconds = options.AutoSubmitIntervalSeconds, BatchSize = options.AutoSubmitBatchSize };

    protected override async Task<List<Guid>> ListDueAsync(ISender sender, IReadOnlyCollection<Guid> deferredIds, CancellationToken cancellationToken) => await sender.Send(new GetExpiredExamSessionIdsQuery(deferredIds), cancellationToken).ConfigureAwait(false);

    protected override async Task ProcessAsync(ISender sender, Guid id, CancellationToken cancellationToken) => await sender.Send(new AutoSubmitExamCommand(id), cancellationToken).ConfigureAwait(false);
}
