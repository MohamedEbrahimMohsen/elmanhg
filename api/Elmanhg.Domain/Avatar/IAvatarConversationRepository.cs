using Core.DDD.Repositories;

namespace Elmanhg.Domain.Avatar;

public interface IAvatarConversationRepository : IRepository<AvatarConversation>
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken);

    Task EraseMessagesAsync(Guid conversationId, CancellationToken cancellationToken);
}
