namespace Elmanhg.Application.Shared.AiService;

public interface IAiEssayGradingClient
{
    Task<AiEssayGradingResult> GradeAsync(AiEssayGradingRequest request, CancellationToken cancellationToken);
}
