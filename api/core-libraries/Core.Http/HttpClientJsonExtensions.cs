using Core.Errors;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace Core.Http;

public static class HttpClientJsonExtensions
{
    public static async Task<HttpJsonReply<TResponse>> TrySendJsonAsync<TResponse>(this HttpClient httpClient, HttpJsonCall call, JsonSerializerOptions serializerOptions, CancellationToken cancellationToken)
    {
        using var request = call.ToRequest();
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception.IsTransientFailure(cancellationToken))
        {
            return new(default, HttpCallFailure.Unreachable, null, exception);
        }

        using (response)
        {
            var statusCode = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                return new(default, HttpCallFailure.Rejected, statusCode, null);
            }

            try
            {
                var value = await response.Content.ReadFromJsonAsync<TResponse>(serializerOptions, cancellationToken).ConfigureAwait(false);
                return new(value, HttpCallFailure.None, statusCode, null);
            }
            catch (JsonException exception)
            {
                return new(default, HttpCallFailure.Unreadable, statusCode, exception);
            }
        }
    }

    public static async Task<TResponse?> SendJsonAsync<TResponse>(this HttpClient httpClient, HttpJsonCall call, JsonSerializerOptions serializerOptions, string errorCode, ILogger logger, CancellationToken cancellationToken)
    {
        var reply = await httpClient.TrySendJsonAsync<TResponse>(call, serializerOptions, cancellationToken).ConfigureAwait(false);
        if (reply.Succeeded)
        {
            return reply.Value;
        }

        logger.LogError(reply.Exception, "Outbound {Method} {Path} failed: {Failure}, HTTP {StatusCode}.", call.Method, call.Path, reply.Failure, reply.StatusCode);
        throw new ServiceUnavailableCoreException(errorCode, innerException: reply.Exception);
    }
}
