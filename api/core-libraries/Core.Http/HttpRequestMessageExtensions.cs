namespace Core.Http;

public static class HttpRequestMessageExtensions
{
    public static HttpRequestMessage WithUserAgent(this HttpRequestMessage request, string userAgent)
    {
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            request.Headers.UserAgent.ParseAdd(userAgent);
        }

        return request;
    }
}
