using Core.Http;
using Core.Messaging.WhatsApp;
using Elmanhg.Tests.Core.Http;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using System.Net;

namespace Elmanhg.Tests.Core.Messaging;

public sealed class MetaWhatsAppClientTests
{
    private static readonly MetaWhatsAppSender Sender = new("v23.0", "123456", "not-a-secret-meta-token");
    private static readonly MetaWhatsAppTemplateMessage Message = MetaWhatsAppTemplateMessage.Create("201012345678", "probe_template", "ar", ["482913"], null);

    private readonly StubHttpMessageHandler _handler = new();

    [Fact]
    public async Task SendTemplateAsync_Message_PostsToVersionedMessagesPathWithBearerAndUserAgent()
    {
        await Client().SendTemplateAsync(Message, Sender, TestContext.Current.CancellationToken);

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("https://graph.facebook.com/v23.0/123456/messages"));
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be("Bearer not-a-secret-meta-token");
        _handler.LastRequest.Headers.UserAgent.ToString().Should().Be("Elmanhg/1.0");
    }

    [Fact]
    public async Task SendTemplateAsync_ProviderRejects_ReturnsRejected()
    {
        _handler.StatusCode = HttpStatusCode.Unauthorized;

        var result = await Client().SendTemplateAsync(Message, Sender, TestContext.Current.CancellationToken);

        result.Failure.Should().Be(HttpCallFailure.Rejected);
        result.StatusCode.Should().Be(401);
    }

    private MetaWhatsAppClient Client() => new(new HttpClient(_handler) { BaseAddress = new Uri("https://graph.facebook.com/") }, CoreHttpTestSettings.Create());
}
