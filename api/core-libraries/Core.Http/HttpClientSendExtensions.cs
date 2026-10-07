namespace Core.Http;

public static class HttpClientSendExtensions
{
    public static async Task<HttpSendResult> TrySendAsync(this HttpClient httpClient, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return new(response.IsSuccessStatusCode ? HttpCallFailure.None : HttpCallFailure.Rejected, (int)response.StatusCode, null);
        }
        catch (Exception exception) when (exception.IsTransientFailure(cancellationToken))
        {
            return new(HttpCallFailure.Unreachable, null, exception);
        }
    }
}
