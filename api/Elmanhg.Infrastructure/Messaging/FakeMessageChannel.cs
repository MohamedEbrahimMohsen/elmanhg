using Elmanhg.Application.Shared.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elmanhg.Infrastructure.Messaging;

public sealed class FakeMessageChannel(MessageChannel channel, ILogger<FakeMessageChannel> logger, IHostEnvironment hostEnvironment) : IMessageChannel
{
    public MessageChannel Channel => channel;

    public Task<bool> SendAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        if (hostEnvironment.IsDevelopment())
        {
            logger.LogInformation("FakeMessageChannel {Channel}: {MessageType} for user {UserId} was not delivered.", channel, message.GetType().Name, message.RecipientUserId);
        }
        else
        {
            logger.LogWarning("FakeMessageChannel {Channel} is active outside Development; the {MessageType} for user {UserId} was not delivered.", channel, message.GetType().Name, message.RecipientUserId);
        }

        return Task.FromResult(true);
    }
}
