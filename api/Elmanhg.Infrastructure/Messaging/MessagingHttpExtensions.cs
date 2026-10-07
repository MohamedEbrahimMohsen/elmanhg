using Core.Http;
using Elmanhg.Application.Shared.Messaging;
using Microsoft.Extensions.Logging;

namespace Elmanhg.Infrastructure.Messaging;

public static class MessagingHttpExtensions
{
    public static async Task<bool> ToDeliveredAsync(this Task<HttpSendResult> send, MessageChannel channel, string messageType, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var result = await send.ConfigureAwait(false);
            if (result.Succeeded)
            {
                return true;
            }

            if (result.Failure == HttpCallFailure.Rejected)
            {
                logger.LogWarning("{MessageType} over {Channel} was rejected with HTTP {StatusCode}.", messageType, channel, result.StatusCode);
                return false;
            }

            logger.LogWarning(result.Exception, "{MessageType} over {Channel} failed before a response arrived.", messageType, channel);
            return false;
        }
        // A reminder failure must never fail the SLA sweep, so every provider fault becomes false; only caller cancellation propagates.
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "{MessageType} over {Channel} failed before a response arrived.", messageType, channel);
            return false;
        }
    }
}
