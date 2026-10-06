using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Infrastructure.AiService;
using Elmanhg.Tests.Core.Http;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace Elmanhg.Tests.Infrastructure.AiService;

public sealed class HttpAiServiceClientTests
{
    private const string ReplyBody = "{\"reply\":\"r\",\"model\":\"claude-sonnet-5\",\"promptVersion\":\"v1\",\"inputTokens\":10,\"outputTokens\":5,\"stopReason\":\"end_turn\",\"citations\":[\"explanation-1\"],\"costUsd\":0.000105}";

    private const string EmbeddingsBody = "{\"model\":\"text-embedding-3-small\",\"dimensions\":2,\"embeddings\":[[0.6,0.8],[1.0,0.0]],\"inputTokens\":4}";

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

        reply.Reply.Should().Be("r");
        reply.Model.Should().Be("claude-sonnet-5");
        reply.PromptVersion.Should().Be("v1");
        reply.InputTokens.Should().Be(10);
        reply.OutputTokens.Should().Be(5);
        reply.StopReason.Should().Be("end_turn");
        reply.Citations.Should().Equal("explanation-1");
        reply.CostUsd.Should().Be(0.000105m);
    }

    [Fact]
    public async Task ChatAsync_WithSources_SendsSourcesArray()
    {
        await ChatAsync();

        using var body = JsonDocument.Parse(_handler.LastBody!);
        var source = body.RootElement.GetProperty("sources")[0];
        source.GetProperty("reference").GetString().Should().Be("explanation-1");
        source.GetProperty("title").GetString().Should().Be("الشرح — قانون أوم");
        source.GetProperty("content").GetString().Should().Be("V = IR");
    }

    [Fact]
    public async Task ChatAsync_ReplyWithoutCitations_ReturnsEmptyCitations()
    {
        _handler.ResponseBody = ReplyBody.Replace(",\"citations\":[\"explanation-1\"]", string.Empty, StringComparison.Ordinal);

        var reply = await ChatAsync();

        reply.Citations.Should().NotBeNull().And.BeEmpty();
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

    [Fact]
    public async Task EmbedAsync_ValidRequest_PostsCamelCaseBodyToV1Embeddings()
    {
        _handler.ResponseBody = EmbeddingsBody;

        await EmbedAsync();

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("http://ai.test/v1/embeddings"));
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be($"Bearer {AiServiceTestSettings.ServiceToken}");
        using var body = JsonDocument.Parse(_handler.LastBody!);
        body.RootElement.GetProperty("inputType").GetString().Should().Be("query");
        body.RootElement.GetProperty("texts").EnumerateArray().Select(x => x.GetString()).Should().Equal("a", "b");
    }

    [Fact]
    public async Task EmbedAsync_Success_ReturnsVectorsModelAndTokens()
    {
        _handler.ResponseBody = EmbeddingsBody;

        var result = await EmbedAsync();

        result.Model.Should().Be("text-embedding-3-small");
        result.Dimensions.Should().Be(2);
        result.Embeddings.Should().HaveCount(2);
        result.Embeddings[0].Should().Equal(0.6f, 0.8f);
        result.Embeddings[1].Should().Equal(1f, 0f);
        result.InputTokens.Should().Be(4);
    }

    [Fact]
    public async Task EmbedAsync_CountMismatch_ThrowsAiServiceUnavailable()
    {
        _handler.ResponseBody = "{\"model\":\"m\",\"dimensions\":2,\"embeddings\":[[0.6,0.8]],\"inputTokens\":4}";

        await ExpectEmbedUnavailableAsync();
    }

    [Fact]
    public async Task EmbedAsync_VectorLengthNotDimensions_ThrowsAiServiceUnavailable()
    {
        _handler.ResponseBody = "{\"model\":\"m\",\"dimensions\":2,\"embeddings\":[[0.6,0.8],[1.0]],\"inputTokens\":4}";

        await ExpectEmbedUnavailableAsync();
    }

    [Fact]
    public async Task EmbedAsync_ServerError_ThrowsAiServiceUnavailable()
    {
        _handler.StatusCode = HttpStatusCode.BadGateway;

        await ExpectEmbedUnavailableAsync();
    }

    private async Task ExpectEmbedUnavailableAsync()
    {
        var act = () => EmbedAsync();

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AiServiceUnavailable);
    }

    private Task<AiEmbeddingResult> EmbedAsync()
    {
        var client = new HttpAiServiceClient(new HttpClient(_handler) { BaseAddress = new Uri("http://ai.test/") }, Options.Create(AiServiceTestSettings.WithHttp()), CoreHttpTestSettings.Create(), NullLogger<HttpAiServiceClient>.Instance);
        return client.EmbedAsync(new AiEmbeddingRequest(AiEmbeddingInputType.Query, ["a", "b"]), TestContext.Current.CancellationToken);
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
        var client = new HttpAiServiceClient(new HttpClient(_handler) { BaseAddress = new Uri("http://ai.test/") }, Options.Create(AiServiceTestSettings.WithHttp()), CoreHttpTestSettings.Create(), NullLogger<HttpAiServiceClient>.Instance);
        return client.ChatAsync(AiServiceTestSettings.ChatRequest(), cancellationToken ?? TestContext.Current.CancellationToken);
    }
}
