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

public sealed class HttpAiTranscriptionClientTests
{
    private const string ReplyBody = "{\"text\":\"نص\",\"model\":\"whisper-1\",\"language\":\"ar\"}";
    private static readonly byte[] Audio = [0x1A, 0x45, 0xDF, 0xA3, 0xFF];
    private readonly StubHttpMessageHandler _handler = new() { ResponseBody = ReplyBody };

    [Fact]
    public async Task TranscribeAsync_Success_PostsBase64AudioWithBearerAndMapsReply()
    {
        var result = await TranscribeAsync();

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("http://ai.test/v1/transcriptions"));
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be($"Bearer {AiServiceTestSettings.ServiceToken}");
        using var body = JsonDocument.Parse(_handler.LastBody!);
        var root = body.RootElement;
        (root.GetProperty("audio").GetString(), root.GetProperty("contentType").GetString(), root.GetProperty("language").GetString(), root.GetProperty("durationSeconds").GetInt32()).Should().Be((Convert.ToBase64String(Audio), "audio/webm", "ar", 12));
        result.Should().Be(new AiTranscriptionResult("نص", "whisper-1", "ar"));
    }

    [Fact]
    public async Task TranscribeAsync_EmptyText_IsAccepted()
    {
        _handler.ResponseBody = "{\"text\":\"\",\"model\":\"whisper-1\",\"language\":\"ar\"}";

        var result = await TranscribeAsync();

        result.Text.Should().BeEmpty();
    }

    [Fact]
    public async Task TranscribeAsync_Non2xx_ThrowsAiServiceUnavailable()
    {
        _handler.StatusCode = HttpStatusCode.ServiceUnavailable;

        await ExpectUnavailableAsync();
    }

    [Fact]
    public async Task TranscribeAsync_TransportFailure_ThrowsAiServiceUnavailable()
    {
        _handler.Throw = new HttpRequestException();

        var exception = await ExpectUnavailableAsync();

        exception.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"model\":\"m\"}")]
    [InlineData("{\"text\":\"x\",\"model\":\"\"}")]
    public async Task TranscribeAsync_InvalidReply_ThrowsAiServiceUnavailable(string body)
    {
        _handler.ResponseBody = body;

        await ExpectUnavailableAsync();
    }

    private async Task<ServiceUnavailableCoreException> ExpectUnavailableAsync()
    {
        var act = () => TranscribeAsync();

        var exception = (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which;
        exception.ErrorCode.Should().Be(ErrorCodes.AiServiceUnavailable);
        return exception;
    }

    private Task<AiTranscriptionResult> TranscribeAsync()
    {
        var client = new HttpAiTranscriptionClient(new HttpClient(_handler) { BaseAddress = new Uri("http://ai.test/") }, Options.Create(AiServiceTestSettings.WithHttp()), NullLogger<HttpAiTranscriptionClient>.Instance);
        return client.TranscribeAsync(new AiTranscriptionRequest(Audio, "audio/webm", "ar", 12), TestContext.Current.CancellationToken);
    }
}
