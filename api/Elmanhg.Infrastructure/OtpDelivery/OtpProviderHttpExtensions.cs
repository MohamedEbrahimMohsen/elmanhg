using Core.Errors;
using Core.OTP.Delivery;
using Elmanhg.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Polly;

namespace Elmanhg.Infrastructure.OtpDelivery;

public static class OtpProviderHttpExtensions
{
    public const string UserAgent = "Elmanhg/1.0";

    public static async Task SendOtpRequestAsync(this HttpClient httpClient, HttpRequestMessage request, OtpChannel channel, ILogger logger, CancellationToken cancellationToken)
    {
        request.Headers.UserAgent.ParseAdd(UserAgent);
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or ExecutionRejectedException || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(exception, "OTP delivery over {Channel} failed before a response arrived.", channel);
            throw new ServiceUnavailableCoreException(ErrorCodes.OtpDeliveryFailed, innerException: exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("OTP delivery over {Channel} was rejected with HTTP {StatusCode}.", channel, (int)response.StatusCode);
                throw new ServiceUnavailableCoreException(ErrorCodes.OtpDeliveryFailed);
            }
        }
    }
}
