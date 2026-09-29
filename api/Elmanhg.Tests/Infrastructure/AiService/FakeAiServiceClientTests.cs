using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Infrastructure.AiService;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Elmanhg.Tests.Infrastructure.AiService;

public sealed class FakeAiServiceClientTests
{
    private const char Fatha = (char)0x064E;
    private const char Kasra = (char)0x0650;

    private readonly IHostEnvironment _hostEnvironment = Substitute.For<IHostEnvironment>();

    [Fact]
    public async Task ChatAsync_Development_ReturnsFakeReply()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Development);

        var reply = await new FakeAiServiceClient(_hostEnvironment).ChatAsync(AiServiceTestSettings.ChatRequest(), TestContext.Current.CancellationToken);

        reply.Reply.Should().Be(FakeAiServiceClient.FakeReply);
        reply.Model.Should().Be("fake");
    }

    [Fact]
    public async Task ChatAsync_WithSources_CitesFirstSource()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Development);

        var reply = await new FakeAiServiceClient(_hostEnvironment).ChatAsync(AiServiceTestSettings.ChatRequest(), TestContext.Current.CancellationToken);

        reply.Citations.Should().Equal("explanation-1");
    }

    [Fact]
    public async Task ChatAsync_WithoutSources_ReturnsNoCitations()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Development);

        var reply = await new FakeAiServiceClient(_hostEnvironment).ChatAsync(AiServiceTestSettings.ChatRequest() with { Sources = [] }, TestContext.Current.CancellationToken);

        reply.Citations.Should().BeEmpty();
    }

    [Fact]
    public async Task ChatAsync_Production_ThrowsAiServiceUnavailable()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Production);

        var act = () => new FakeAiServiceClient(_hostEnvironment).ChatAsync(AiServiceTestSettings.ChatRequest(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AiServiceUnavailable);
    }

    [Fact]
    public async Task EmbedAsync_Development_ReturnsUnitVectorsOfChunkDimensions()
    {
        var result = await EmbedAsync("قانون أوم", string.Empty);

        result.Model.Should().Be("fake");
        result.Dimensions.Should().Be(LessonContentChunk.EmbeddingDimensions);
        result.Embeddings.Should().HaveCount(2).And.OnlyContain(x => x.Length == LessonContentChunk.EmbeddingDimensions);
        result.Embeddings.Select(Norm).Should().OnlyContain(x => Math.Abs(x - 1) < 1e-5);
        result.Embeddings[1][0].Should().Be(1f);
    }

    [Fact]
    public async Task EmbedAsync_SameTextWithAndWithoutTashkeel_ReturnsSameVector()
    {
        var result = await EmbedAsync("قانون المقاومة", $"ق{Fatha}انون الم{Kasra}قاومة");

        result.Embeddings[0].Should().Equal(result.Embeddings[1]);
    }

    [Fact]
    public async Task EmbedAsync_SharedWords_ScoreHigherThanUnrelated()
    {
        var result = await EmbedAsync("ما هو قانون أوم", "قانون أوم يربط الجهد بالتيار", "الخلية النباتية لها جدار");

        Cosine(result.Embeddings[0], result.Embeddings[1]).Should().BeGreaterThan(Cosine(result.Embeddings[0], result.Embeddings[2]));
    }

    [Fact]
    public async Task EmbedAsync_Production_ThrowsAiServiceUnavailable()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Production);

        var act = () => new FakeAiServiceClient(_hostEnvironment).EmbedAsync(new AiEmbeddingRequest(AiEmbeddingInputType.Query, ["q"]), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AiServiceUnavailable);
    }

    private Task<AiEmbeddingResult> EmbedAsync(params string[] texts)
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Development);
        return new FakeAiServiceClient(_hostEnvironment).EmbedAsync(new AiEmbeddingRequest(AiEmbeddingInputType.Document, texts), TestContext.Current.CancellationToken);
    }

    private static double Norm(float[] vector) => Math.Sqrt(vector.Sum(x => (double)x * x));

    private static double Cosine(float[] left, float[] right) => left.Zip(right, (x, y) => (double)x * y).Sum();
}
