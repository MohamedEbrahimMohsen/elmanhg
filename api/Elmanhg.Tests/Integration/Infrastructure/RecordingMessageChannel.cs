using Elmanhg.Application.Shared.Messaging;

namespace Elmanhg.Tests.Integration.Infrastructure;

public sealed class RecordingMessageChannel(MessageChannel channel, MessageOutbox outbox) : IMessageChannel
{
    public MessageChannel Channel => channel;

    public Task<bool> SendAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        outbox.Record(channel, message);
        return Task.FromResult(true);
    }
}
