using Core.Http;
using Core.Messaging.Email;
using Elmanhg.Tests.Core.Http;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using System.Net;
using System.Text.Json;

namespace Elmanhg.Tests.Core.Messaging;

public sealed class ResendEmailClientTests
{
    private const string ApiKey = "not-a-secret-resend-key";
    private const string IdempotencyKey = "probe-idempotency-key";
    private static readonly EmailMessage Message = new("Elmanhg <otp@elmanhg.test>", "mona@elmanhg.test", "Probe subject", "<p>probe</p>", "probe");

    private readonly StubHttpMessageHandler _handler = new();

    [Fact]
    public async Task SendAsync_Message_PostsToEmailsWithBearerIdempotencyKeyAndUserAgent()
    {
        await Client().SendAsync(Message, ApiKey, IdempotencyKey, TestContext.Current.CancellationToken);

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("https://api.resend.com/emails"));
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be("Bearer not-a-secret-resend-key");
        _handler.LastRequest.Headers.GetValues("Idempotency-Key").Single().Should().Be(IdempotencyKey);
        _handler.LastRequest.Headers.UserAgent.ToString().Should().Be("Elmanhg/1.0");
    }

    [Fact]
    public async Task SendAsync_Message_SendsResendJsonShape()
    {
        await Client().SendAsync(Message, ApiKey, IdempotencyKey, TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(_handler.LastBody!);
        var root = body.RootElement;
        root.GetProperty("from").GetString().Should().Be(Message.From);
        root.GetProperty("to").EnumerateArray().Select(x => x.GetString()).Should().Equal(Message.To);
        root.GetProperty("subject").GetString().Should().Be(Message.Subject);
        root.GetProperty("html").GetString().Should().Be(Message.Html);
        root.GetProperty("text").GetString().Should().Be(Message.Text);
    }

    [Fact]
    public async Task SendAsync_ProviderRejects_ReturnsRejected()
    {
        _handler.StatusCode = HttpStatusCode.UnprocessableEntity;

        var result = await Client().SendAsync(Message, ApiKey, IdempotencyKey, TestContext.Current.CancellationToken);

        result.Failure.Should().Be(HttpCallFailure.Rejected);
        result.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task SendAsync_TransportFailure_ReturnsUnreachable()
    {
        _handler.Throw = new HttpRequestException("connection refused");

        var result = await Client().SendAsync(Message, ApiKey, IdempotencyKey, TestContext.Current.CancellationToken);

        result.Failure.Should().Be(HttpCallFailure.Unreachable);
    }

    private ResendEmailClient Client() => new(new HttpClient(_handler) { BaseAddress = new Uri("https://api.resend.com/") }, CoreHttpTestSettings.Create());
}
