using Core.Queues;
using Elmanhg.Application.MathStepGrading.ApplyMathStepGrade;
using Elmanhg.Application.MathStepGrading.CheckMathStepAnswer;
using Elmanhg.Application.MathStepGrading.FailMathStepGrade;
using Elmanhg.Application.MathStepGrading.GetDueMathStepGradeIds;
using Elmanhg.Application.MathStepGrading.GradeMathSteps;
using Elmanhg.Application.Shared.Options;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class MathStepGradingWorker(IServiceScopeFactory scopeFactory, IOptions<MathStepGradingOptions> mathStepGradingOptions, TimeProvider timeProvider, ILogger<MathStepGradingWorker> logger, BackgroundJobMetrics jobMetrics) : SweepWorker<MathStepGradingOptions>(scopeFactory, mathStepGradingOptions, timeProvider, logger, jobMetrics)
{
    protected override string JobName => "math-step-grading";

    protected override SweepOptions SweepOptionsOf(MathStepGradingOptions options) => new() { Enabled = options.SweepEnabled, IntervalSeconds = options.SweepIntervalSeconds, BatchSize = options.SweepBatchSize };

    protected override async Task<List<Guid>> ListDueAsync(ISender sender, IReadOnlyCollection<Guid> deferredIds, CancellationToken cancellationToken) => await sender.Send(new GetDueMathStepGradeIdsQuery(), cancellationToken).ConfigureAwait(false);

    protected override async Task ProcessAsync(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new CheckMathStepAnswerCommand(id), cancellationToken).ConfigureAwait(false);
        await sender.Send(new GradeMathStepsCommand(id), cancellationToken).ConfigureAwait(false);
        await sender.Send(new ApplyMathStepGradeCommand(id), cancellationToken).ConfigureAwait(false);
    }

    protected override async Task RecordFailureAsync(ISender sender, Guid id, string errorCode, CancellationToken cancellationToken) => await sender.Send(new FailMathStepGradeCommand(id, errorCode), cancellationToken).ConfigureAwait(false);
}
