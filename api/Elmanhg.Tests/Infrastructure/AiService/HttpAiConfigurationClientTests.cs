using Elmanhg.Application.Configuration.Shared;
using Elmanhg.Infrastructure.AiService;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;

namespace Elmanhg.Tests.Infrastructure.AiService;

public sealed class HttpAiConfigurationClientTests
{
    public const string ReplyBody = "{\"llmProvider\":\"openai_compatible\",\"chatModel\":\"gpt-5.6-luna\",\"essayGradingModel\":\"gpt-5.6-luna\",\"mathStepGradingModel\":\"gpt-5.6-luna\",\"embeddingProvider\":\"openai\",\"embeddingModel\":\"text-embedding-3-small\",\"transcriptionProvider\":\"openai\",\"transcriptionModel\":\"whisper-1\",\"secrets\":[{\"key\":\"ELMANHG_AI_OPENAI_API_KEY\",\"isSet\":true}]}";
    private readonly StubHttpMessageHandler _handler = new() { ResponseBody = ReplyBody };

    [Fact]
    public async Task GetAsync_Success_SendsBearerGetAndMapsReply()
    {
        var result = await GetAsync();

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("http://ai.test/v1/configuration"));
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be($"Bearer {AiServiceTestSettings.ServiceToken}");
        result.Should().BeEquivalentTo(new AiServiceConfigurationResult("openai_compatible", "gpt-5.6-luna", "gpt-5.6-luna", "gpt-5.6-luna", "openai", "text-embedding-3-small", "openai", "whisper-1", [new SecretStatusResult("ELMANHG_AI_OPENAI_API_KEY", true)]));
    }

    [Fact]
    public async Task GetAsync_Non2xx_ReturnsNull()
    {
        _handler.StatusCode = HttpStatusCode.ServiceUnavailable;

        var result = await GetAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_TransportFailure_ReturnsNull()
    {
        _handler.Throw = new HttpRequestException();

        var result = await GetAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_MalformedJson_ReturnsNull()
    {
        _handler.ResponseBody = "not json";

        var result = await GetAsync();

        result.Should().BeNull();
    }

    private Task<AiServiceConfigurationResult?> GetAsync()
    {
        var client = new HttpAiConfigurationClient(new HttpClient(_handler) { BaseAddress = new Uri("http://ai.test/") }, Options.Create(AiServiceTestSettings.WithHttp()), NullLogger<HttpAiConfigurationClient>.Instance);
        return client.GetAsync(TestContext.Current.CancellationToken);
    }
}
