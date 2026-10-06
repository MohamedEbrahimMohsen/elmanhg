using Core.Http;
using Core.Messaging.Sms;
using Elmanhg.Tests.Core.Http;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using System.Net;

namespace Elmanhg.Tests.Core.Messaging;

public sealed class HttpSmsClientTests
{
    private const string Phone = "01012345678";
    private const string InternationalPhone = "201012345678";
    private const string Message = "code 482913";

    private readonly StubHttpMessageHandler _handler = new();

    [Fact]
    public async Task SendAsync_JsonGateway_PostsRenderedBodyContentTypeAndAuthHeader()
    {
        await Client().SendAsync(Gateway("X-Api-Key", "not-a-secret-sms-key"), Phone, InternationalPhone, Message, TestContext.Current.CancellationToken);

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("https://sms.example.test/send"));
        _handler.LastRequest.Content!.Headers.ContentType!.MediaType.Should().Be(HttpSmsBodyRenderer.JsonContentType);
        _handler.LastBody.Should().Be("{\"to\":\"201012345678\",\"text\":\"code 482913\"}");
        _handler.LastRequest.Headers.GetValues("X-Api-Key").Single().Should().Be("not-a-secret-sms-key");
        _handler.LastRequest.Headers.UserAgent.ToString().Should().Be("Elmanhg/1.0");
    }

    [Fact]
    public async Task SendAsync_NoAuthHeader_SendsNoCustomHeader()
    {
        await Client().SendAsync(Gateway(string.Empty, string.Empty), Phone, InternationalPhone, Message, TestContext.Current.CancellationToken);

        _handler.LastRequest!.Headers.Contains("X-Api-Key").Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_ProviderRejects_ReturnsRejected()
    {
        _handler.StatusCode = HttpStatusCode.InternalServerError;

        var result = await Client().SendAsync(Gateway("X-Api-Key", "not-a-secret-sms-key"), Phone, InternationalPhone, Message, TestContext.Current.CancellationToken);

        result.Failure.Should().Be(HttpCallFailure.Rejected);
    }

    private static HttpSmsGateway Gateway(string authHeaderName, string authHeaderValue) => new("https://sms.example.test/send", HttpSmsBodyRenderer.JsonContentType, "{\"to\":\"{internationalPhoneNumber}\",\"text\":\"{message}\"}", authHeaderName, authHeaderValue);

    private HttpSmsClient Client() => new(new HttpClient(_handler), CoreHttpTestSettings.Create());
}
