using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Core.Http;

public sealed record HttpJsonCall(HttpMethod Method, string Path, HttpContent? Content, AuthenticationHeaderValue? Authorization, string UserAgent)
{
    public static HttpJsonCall Post<TRequest>(string path, TRequest body, JsonSerializerOptions serializerOptions, AuthenticationHeaderValue? authorization, string userAgent) => new(HttpMethod.Post, path, JsonContent.Create(body, options: serializerOptions), authorization, userAgent);

    public static HttpJsonCall Get(string path, AuthenticationHeaderValue? authorization, string userAgent) => new(HttpMethod.Get, path, null, authorization, userAgent);

    public HttpRequestMessage ToRequest()
    {
        var request = new HttpRequestMessage(Method, Path) { Content = Content };
        request.Headers.Authorization = Authorization;
        return request.WithUserAgent(UserAgent);
    }
}
