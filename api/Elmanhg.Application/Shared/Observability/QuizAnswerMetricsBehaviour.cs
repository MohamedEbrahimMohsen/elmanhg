using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Application.Sessions.SubmitAnswer;
using MediatR;

namespace Elmanhg.Application.Shared.Observability;

public sealed class QuizAnswerMetricsBehaviour(ElmanhgMetrics metrics) : IPipelineBehavior<SubmitAnswerCommand, SessionItemResult>
{
    private const string UngradedOutcome = "Ungraded";

    public async Task<SessionItemResult> Handle(SubmitAnswerCommand request, RequestHandlerDelegate<SessionItemResult> next, CancellationToken cancellationToken)
    {
        var result = await next(cancellationToken).ConfigureAwait(false);
        metrics.RecordQuizAnswer(result.Attempt?.Outcome ?? UngradedOutcome);
        return result;
    }
}
