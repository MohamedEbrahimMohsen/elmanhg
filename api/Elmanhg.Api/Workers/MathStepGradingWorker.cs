using Core.Errors;
using Elmanhg.Application.MathStepGrading.ApplyMathStepGrade;
using Elmanhg.Application.MathStepGrading.CheckMathStepAnswer;
using Elmanhg.Application.MathStepGrading.FailMathStepGrade;
using Elmanhg.Application.MathStepGrading.GetDueMathStepGradeIds;
using Elmanhg.Application.MathStepGrading.GradeMathSteps;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Shared.Options;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class MathStepGradingWorker(IServiceScopeFactory scopeFactory, IOptions<MathStepGradingOptions> mathStepGradingOptions, TimeProvider timeProvider, ILogger<MathStepGradingWorker> logger, BackgroundJobMetrics jobMetrics) : BackgroundService
{
    private const string JobName = "math-step-grading";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = mathStepGradingOptions.Value;
        if (!options.SweepEnabled)
        {
            return;
        }

        jobMetrics.Register(JobName, TimeSpan.FromSeconds(options.SweepIntervalSeconds));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.SweepIntervalSeconds), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await SweepAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task SweepAsync(CancellationToken stoppingToken)
    {
        using var run = jobMetrics.StartRun(JobName);
        var gradeIds = await ListDueAsync(stoppingToken).ConfigureAwait(false);
        if (gradeIds is null)
        {
            run.MarkListingFailed();
            return;
        }

        foreach (var gradeId in gradeIds)
        {
            if (await GradeAsync(gradeId, stoppingToken).ConfigureAwait(false))
            {
                run.ItemSucceeded();
                continue;
            }

            run.ItemFailed();
        }
    }

    private async Task<List<Guid>?> ListDueAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetDueMathStepGradeIdsQuery(), stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Listing due math step grades failed.");
            return null;
        }
    }

    private async Task<bool> GradeAsync(Guid gradeId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CheckMathStepAnswerCommand(gradeId), stoppingToken).ConfigureAwait(false);
            await sender.Send(new GradeMathStepsCommand(gradeId), stoppingToken).ConfigureAwait(false);
            await sender.Send(new ApplyMathStepGradeCommand(gradeId), stoppingToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Grading of math steps {MathStepGradeId} failed.", gradeId);
            await RecordFailureAsync(gradeId, ErrorCodeOf(exception), stoppingToken).ConfigureAwait(false);
            return false;
        }
    }

    private async Task RecordFailureAsync(Guid gradeId, string errorCode, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new FailMathStepGradeCommand(gradeId, errorCode), stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Recording the failed grading of math steps {MathStepGradeId} failed.", gradeId);
        }
    }

    private static string ErrorCodeOf(Exception exception) => exception is BaseException { ErrorCode: { Length: > 0 } code } ? code : exception.GetType().Name;
}
