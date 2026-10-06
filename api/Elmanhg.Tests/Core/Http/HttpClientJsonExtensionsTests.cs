using Core.Errors;
using Core.Http;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Elmanhg.Tests.Core.Http;

public sealed class HttpClientJsonExtensionsTests
{
    private const string ErrorCode = "NOT_A_REAL_CODE";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly AuthenticationHeaderValue Authorization = new("Bearer", "not-a-secret-token");

    private readonly StubHttpMessageHandler _handler = new() { ResponseBody = "{\"answer\":\"forty-two\"}" };

    [Fact]
    public async Task TrySendJsonAsync_PostCall_SendsMethodPathBodyAuthorizationAndUserAgent()
    {
        await Client().TrySendJsonAsync<ProbeReply>(PostCall(), SerializerOptions, TestContext.Current.CancellationToken);

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("https://provider.test/v1/probe"));
        _handler.LastBody.Should().Be("{\"questionText\":\"what?\"}");
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be("Bearer not-a-secret-token");
        _handler.LastRequest.Headers.UserAgent.ToString().Should().Be("Elmanhg/1.0");
    }

    [Fact]
    public async Task TrySendJsonAsync_GetCall_SendsNoBody()
    {
        await Client().TrySendJsonAsync<ProbeReply>(HttpJsonCall.Get("v1/probe", Authorization, CoreHttpTestSettings.UserAgent), SerializerOptions, TestContext.Current.CancellationToken);

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        _handler.LastBody.Should().BeNull();
    }

    [Fact]
    public async Task TrySendJsonAsync_Success_ReturnsDeserializedValue()
    {
        var reply = await Client().TrySendJsonAsync<ProbeReply>(PostCall(), SerializerOptions, TestContext.Current.CancellationToken);

        reply.Succeeded.Should().BeTrue();
        reply.Value.Should().Be(new ProbeReply("forty-two"));
    }

    [Fact]
    public async Task TrySendJsonAsync_ErrorStatus_ReturnsRejected()
    {
        _handler.StatusCode = HttpStatusCode.ServiceUnavailable;

        var reply = await Client().TrySendJsonAsync<ProbeReply>(PostCall(), SerializerOptions, TestContext.Current.CancellationToken);

        reply.Failure.Should().Be(HttpCallFailure.Rejected);
        reply.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task TrySendJsonAsync_MalformedJson_ReturnsUnreadable()
    {
        _handler.ResponseBody = "{not json";

        var reply = await Client().TrySendJsonAsync<ProbeReply>(PostCall(), SerializerOptions, TestContext.Current.CancellationToken);

        reply.Failure.Should().Be(HttpCallFailure.Unreadable);
        reply.Exception.Should().BeAssignableTo<JsonException>();
    }

    [Fact]
    public async Task TrySendJsonAsync_TransportFailure_ReturnsUnreachable()
    {
        _handler.Throw = new HttpRequestException("connection refused");

        var reply = await Client().TrySendJsonAsync<ProbeReply>(PostCall(), SerializerOptions, TestContext.Current.CancellationToken);

        reply.Failure.Should().Be(HttpCallFailure.Unreachable);
    }

    [Fact]
    public async Task SendJsonAsync_Success_ReturnsValue()
    {
        var value = await SendAsync();

        value.Should().Be(new ProbeReply("forty-two"));
    }

    [Fact]
    public async Task SendJsonAsync_ErrorStatus_ThrowsServiceUnavailableWithErrorCode()
    {
        _handler.StatusCode = HttpStatusCode.BadGateway;

        var act = SendAsync;

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCode);
    }

    [Fact]
    public async Task SendJsonAsync_TransportFailure_ThrowsWithInnerException()
    {
        _handler.Throw = new HttpRequestException("connection refused");

        var act = SendAsync;

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task SendJsonAsync_MalformedJson_ThrowsServiceUnavailableWithErrorCode()
    {
        _handler.ResponseBody = "{not json";

        var act = SendAsync;

        var exception = (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which;
        exception.ErrorCode.Should().Be(ErrorCode);
        exception.InnerException.Should().BeAssignableTo<JsonException>();
    }

    private Task<ProbeReply?> SendAsync() => Client().SendJsonAsync<ProbeReply>(PostCall(), SerializerOptions, ErrorCode, NullLogger.Instance, TestContext.Current.CancellationToken);

    private HttpClient Client() => new(_handler, disposeHandler: false) { BaseAddress = new Uri("https://provider.test/") };

    private static HttpJsonCall PostCall() => HttpJsonCall.Post("v1/probe", new ProbeRequest("what?"), SerializerOptions, Authorization, CoreHttpTestSettings.UserAgent);

    private sealed record ProbeRequest(string QuestionText);

    private sealed record ProbeReply(string Answer);
}
