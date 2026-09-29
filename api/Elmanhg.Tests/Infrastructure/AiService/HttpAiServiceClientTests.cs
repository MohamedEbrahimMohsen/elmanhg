using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Infrastructure.AiService;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace Elmanhg.Tests.Infrastructure.AiService;

public sealed class HttpAiServiceClientTests
{
    private const string ReplyBody = "{\"reply\":\"r\",\"model\":\"claude-sonnet-5\",\"promptVersion\":\"v1\",\"inputTokens\":10,\"outputTokens\":5,\"stopReason\":\"end_turn\"}";

    private readonly StubHttpMessageHandler _handler = new() { ResponseBody = ReplyBody };

    [Fact]
    public async Task ChatAsync_ValidRequest_PostsToV1ChatWithBearerToken()
    {
        await ChatAsync();

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("http://ai.test/v1/chat"));
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be($"Bearer {AiServiceTestSettings.ServiceToken}");
        _handler.LastRequest.Headers.UserAgent.ToString().Should().Be("Elmanhg/1.0");
    }

    [Fact]
    public async Task ChatAsync_ValidRequest_SendsCamelCaseContractBody()
    {
        await ChatAsync();

        using var body = JsonDocument.Parse(_handler.LastBody!);
        var root = body.RootElement;
        var context = root.GetProperty("context");
        context.GetProperty("entryPoint").GetString().Should().Be("quizQuestion");
        context.GetProperty("lesson").GetProperty("objectives").EnumerateArray().Select(x => x.GetString()).Should().Equal("Apply Ohm's law");
        context.GetProperty("question").GetProperty("studentAnswer").GetString().Should().Be("2");
        context.GetProperty("question").TryGetProperty("explanation", out _).Should().BeFalse();
        root.GetProperty("history")[1].GetProperty("role").GetString().Should().Be("assistant");
        root.GetProperty("message").GetString().Should().Be("why?");
    }

    [Fact]
    public async Task ChatAsync_Success_ReturnsReply()
    {
        var reply = await ChatAsync();

        reply.Should().Be(new AiChatReply("r", "claude-sonnet-5", "v1", 10, 5, "end_turn"));
    }

    [Fact]
    public async Task ChatAsync_ServerError_ThrowsAiServiceUnavailable()
    {
        _handler.StatusCode = HttpStatusCode.ServiceUnavailable;

        await ExpectUnavailableAsync();
    }

    [Fact]
    public async Task ChatAsync_Unauthorized_ThrowsAiServiceUnavailable()
    {
        _handler.StatusCode = HttpStatusCode.Unauthorized;

        await ExpectUnavailableAsync();
    }

    [Fact]
    public async Task ChatAsync_NetworkFailure_ThrowsAiServiceUnavailable()
    {
        _handler.Throw = new HttpRequestException();

        var exception = await ExpectUnavailableAsync();

        exception.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task ChatAsync_InvalidJsonBody_ThrowsAiServiceUnavailable()
    {
        _handler.ResponseBody = "{";

        await ExpectUnavailableAsync();
    }

    [Fact]
    public async Task ChatAsync_BlankReply_ThrowsAiServiceUnavailable()
    {
        _handler.ResponseBody = ReplyBody.Replace("\"reply\":\"r\"", "\"reply\":\" \"", StringComparison.Ordinal);

        await ExpectUnavailableAsync();
    }

    [Fact]
    public async Task ChatAsync_CallerCancelled_ThrowsOperationCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _handler.Throw = new OperationCanceledException(cancellation.Token);

        var act = () => ChatAsync(cancellation.Token);

        (await act.Should().ThrowAsync<OperationCanceledException>()).Which.Should().NotBeOfType<ServiceUnavailableCoreException>();
    }

    private async Task<ServiceUnavailableCoreException> ExpectUnavailableAsync()
    {
        var act = () => ChatAsync();

        var exception = (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which;
        exception.ErrorCode.Should().Be(ErrorCodes.AiServiceUnavailable);
        return exception;
    }

    private Task<AiChatReply> ChatAsync(CancellationToken? cancellationToken = null)
    {
        var client = new HttpAiServiceClient(new HttpClient(_handler) { BaseAddress = new Uri("http://ai.test/") }, Options.Create(AiServiceTestSettings.WithHttp()), NullLogger<HttpAiServiceClient>.Instance);
        return client.ChatAsync(AiServiceTestSettings.ChatRequest(), cancellationToken ?? TestContext.Current.CancellationToken);
    }
}
