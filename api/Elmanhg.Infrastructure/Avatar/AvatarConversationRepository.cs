using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Avatar;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Avatar;

public class AvatarConversationRepository(AppDbContext context) : Repository<AvatarConversation>(context), IAvatarConversationRepository
{
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await operation(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task EraseMessagesAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlAsync($"SELECT erase_avatar_conversation({conversationId})", cancellationToken).ConfigureAwait(false);
    }
}
