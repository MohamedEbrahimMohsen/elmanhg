using System.Net;

namespace Elmanhg.Tests.Infrastructure.OtpDelivery;

public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    public Exception? Throw { get; set; }

    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastBody { get; private set; }

    public int CallCount { get; private set; }

    public List<IReadOnlyDictionary<string, string>> RequestHeaders { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        RequestHeaders.Add(request.Headers.ToDictionary(x => x.Key, x => string.Join(',', x.Value)));
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (Throw is not null)
        {
            throw Throw;
        }

        return new HttpResponseMessage(StatusCode);
    }
}
