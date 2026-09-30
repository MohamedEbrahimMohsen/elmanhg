namespace Elmanhg.Application.Shared.AiService;

public interface IAiMathStepGradingClient
{
    Task<AiMathStepGradingResult> GradeAsync(AiMathStepGradingRequest request, CancellationToken cancellationToken);
}
