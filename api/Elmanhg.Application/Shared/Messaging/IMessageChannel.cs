namespace Elmanhg.Application.Shared.Messaging;

public interface IMessageChannel
{
    MessageChannel Channel { get; }

    /// <summary>Returns false on a provider failure; never throws except <see cref="OperationCanceledException"/>.</summary>
    Task<bool> SendAsync(OutboundMessage message, CancellationToken cancellationToken);
}
