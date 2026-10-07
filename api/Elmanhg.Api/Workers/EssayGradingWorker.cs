using Core.Queues;
using Elmanhg.Application.EssayGrading.ApplyEssayGrade;
using Elmanhg.Application.EssayGrading.FailEssayGrade;
using Elmanhg.Application.EssayGrading.GetDueEssayGradeIds;
using Elmanhg.Application.EssayGrading.GradeEssay;
using Elmanhg.Application.Shared.Options;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class EssayGradingWorker(IServiceScopeFactory scopeFactory, IOptions<EssayGradingOptions> essayGradingOptions, TimeProvider timeProvider, ILogger<EssayGradingWorker> logger, BackgroundJobMetrics jobMetrics) : SweepWorker<EssayGradingOptions>(scopeFactory, essayGradingOptions, timeProvider, logger, jobMetrics)
{
    protected override string JobName => "essay-grading";

    protected override SweepOptions SweepOptionsOf(EssayGradingOptions options) => new() { Enabled = options.SweepEnabled, IntervalSeconds = options.SweepIntervalSeconds, BatchSize = options.SweepBatchSize };

    protected override async Task<List<Guid>> ListDueAsync(ISender sender, IReadOnlyCollection<Guid> deferredIds, CancellationToken cancellationToken) => await sender.Send(new GetDueEssayGradeIdsQuery(), cancellationToken).ConfigureAwait(false);

    protected override async Task ProcessAsync(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new GradeEssayCommand(id), cancellationToken).ConfigureAwait(false);
        await sender.Send(new ApplyEssayGradeCommand(id), cancellationToken).ConfigureAwait(false);
    }

    protected override async Task RecordFailureAsync(ISender sender, Guid id, string errorCode, CancellationToken cancellationToken) => await sender.Send(new FailEssayGradeCommand(id, errorCode), cancellationToken).ConfigureAwait(false);
}
