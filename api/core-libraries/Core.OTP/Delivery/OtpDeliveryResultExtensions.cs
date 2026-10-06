using Core.Errors;
using Core.Http;
using Microsoft.Extensions.Logging;

namespace Core.OTP.Delivery;

public static class OtpDeliveryResultExtensions
{
    public static void EnsureDelivered(this HttpSendResult result, OtpChannel channel, string errorCode, ILogger logger)
    {
        if (result.Succeeded)
        {
            return;
        }

        if (result.Failure == HttpCallFailure.Rejected)
        {
            logger.LogError("OTP delivery over {Channel} was rejected with HTTP {StatusCode}.", channel, result.StatusCode);
            throw new ServiceUnavailableCoreException(errorCode);
        }

        logger.LogError(result.Exception, "OTP delivery over {Channel} failed before a response arrived.", channel);
        throw new ServiceUnavailableCoreException(errorCode, innerException: result.Exception);
    }
}
