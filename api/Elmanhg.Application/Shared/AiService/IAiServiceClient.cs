namespace Elmanhg.Application.Shared.AiService;

public interface IAiServiceClient
{
    Task<AiChatReply> ChatAsync(AiChatRequest request, CancellationToken cancellationToken);
}
