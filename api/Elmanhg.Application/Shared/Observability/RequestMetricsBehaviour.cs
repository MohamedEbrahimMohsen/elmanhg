using Core.CQRS.Behaviours;
using Core.Errors;
using MediatR;
using System.Diagnostics;

namespace Elmanhg.Application.Shared.Observability;

public sealed class RequestMetricsBehaviour<TRequest, TResponse>(ElmanhgMetrics metrics) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private const string ValidationFailedOutcome = "VALIDATION_FAILED";
    private const string CancelledOutcome = "CANCELLED";
    private const string UnhandledOutcome = "UNHANDLED_EXCEPTION";

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var outcome = ElmanhgMetrics.SuccessOutcome;
        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            outcome = OutcomeOf(exception);
            throw;
        }
        finally
        {
            metrics.RecordRequest(typeof(TRequest).Name, outcome, Stopwatch.GetElapsedTime(started));
        }
    }

    private static string OutcomeOf(Exception exception) => exception switch
    {
        ValidationBehaviourException => ValidationFailedOutcome,
        OperationCanceledException => CancelledOutcome,
        BaseException { ErrorCode: { Length: > 0 } code } => code,
        _ => UnhandledOutcome,
    };
}
