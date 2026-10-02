using Elmanhg.Application.Shared.Messaging;
using Elmanhg.Infrastructure.OtpDelivery;
using Microsoft.Extensions.Logging;

namespace Elmanhg.Infrastructure.Messaging;

public static class MessagingHttpExtensions
{
    public static async Task<bool> SendMessageRequestAsync(this HttpClient httpClient, HttpRequestMessage request, MessageChannel channel, string messageType, ILogger logger, CancellationToken cancellationToken)
    {
        request.Headers.UserAgent.ParseAdd(OtpProviderHttpExtensions.UserAgent);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            logger.LogWarning("{MessageType} over {Channel} was rejected with HTTP {StatusCode}.", messageType, channel, (int)response.StatusCode);
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
