namespace Elmanhg.Application.Shared.AiService;

public interface IAiMathCheckClient
{
    Task<AiMathCheckResult> CheckAsync(AiMathCheckRequest request, CancellationToken cancellationToken);
}
