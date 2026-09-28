using Elmanhg.Application.Questions.GetServableQuestionCount;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Questions.GetServableQuestionCount;

public sealed class GetServableQuestionCountHandlerTests : IDisposable
{
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly GetServableQuestionCountHandler _handler;

    public GetServableQuestionCountHandlerTests()
    {
        _questionRepository.CountServableAsync(Arg.Any<CancellationToken>()).Returns(42, 43);
        _handler = new GetServableQuestionCountHandler(_questionRepository, _memoryCache, Options.Create(new ContentOptions { ServableCountCacheSeconds = 60 }));
    }

    [Fact]
    public async Task Handle_ColdCache_ReturnsRepositoryCount()
    {
        var result = await _handler.Handle(new GetServableQuestionCountQuery(), TestContext.Current.CancellationToken);

        result.Count.Should().Be(42);
        _memoryCache.Get<int>(ServableQuestionCountCache.Key).Should().Be(42);
    }

    [Fact]
    public async Task Handle_WarmCache_ReturnsCachedCountWithoutCounting()
    {
        var first = await _handler.Handle(new GetServableQuestionCountQuery(), TestContext.Current.CancellationToken);

        var second = await _handler.Handle(new GetServableQuestionCountQuery(), TestContext.Current.CancellationToken);

        (first.Count, second.Count).Should().Be((42, 42));
        await _questionRepository.Received(1).CountServableAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AfterKeyRemoved_Recounts()
    {
        await _handler.Handle(new GetServableQuestionCountQuery(), TestContext.Current.CancellationToken);
        _memoryCache.Remove(ServableQuestionCountCache.Key);

        var result = await _handler.Handle(new GetServableQuestionCountQuery(), TestContext.Current.CancellationToken);

        result.Count.Should().Be(43);
        await _questionRepository.Received(2).CountServableAsync(Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }
}
