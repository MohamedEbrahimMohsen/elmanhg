using Core.Errors;
using Elmanhg.Application.ContentRetrieval.SearchLessonContent;
using Elmanhg.Application.ContentRetrieval.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.ContentRetrieval.SearchLessonContent;

public sealed class SearchLessonContentHandlerTests
{
    private static readonly DateTimeOffset IndexedAt = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ILessonContentIndexRepository _indexRepository = Substitute.For<ILessonContentIndexRepository>();
    private readonly ILessonContentChunkRepository _chunkRepository = Substitute.For<ILessonContentChunkRepository>();
    private readonly IAiServiceClient _aiServiceClient = Substitute.For<IAiServiceClient>();
    private readonly ContentRetrievalOptions _options = new() { DefaultTopK = 5, MaxTopK = 20 };
    private readonly Lesson _lesson = new QuestionBuilder().Lesson;
    private readonly float[] _queryVector = new float[LessonContentChunk.EmbeddingDimensions];
    private readonly SearchLessonContentHandler _handler;

    public SearchLessonContentHandlerTests()
    {
        _lesson.Publish(Guid.NewGuid());
        _lessonRepository.GetByIdAsync(_lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), true).Returns(_lesson);
        _aiServiceClient.EmbedAsync(Arg.Any<AiEmbeddingRequest>(), Arg.Any<CancellationToken>()).Returns(_ => new AiEmbeddingResult("text-embedding-3-small", LessonContentChunk.EmbeddingDimensions, [_queryVector], 3));
        _handler = new SearchLessonContentHandler(_lessonRepository, _indexRepository, _chunkRepository, _aiServiceClient, Options.Create(_options));
    }

    [Fact]
    public async Task Handle_IndexedLesson_EmbedsQueryAndReturnsScoredMatches()
    {
        GivenIndex(chunkCount: 2);
        var match = new LessonContentMatch(Guid.NewGuid(), LessonContentSection.Summary, "Recap", 1, null, "Resistance", 0.4);
        _chunkRepository.SearchAsync(_lesson.Id, _queryVector, "text-embedding-3-small", 5, true, Arg.Any<CancellationToken>()).Returns([match]);

        var result = await _handler.Handle(new SearchLessonContentQuery(_lesson.Id, "  Ohm's law  ", null), TestContext.Current.CancellationToken);

        await _aiServiceClient.Received(1).EmbedAsync(Arg.Is<AiEmbeddingRequest>(x => x.InputType == AiEmbeddingInputType.Query && x.Texts.SequenceEqual(new[] { "Ohm's law" })), Arg.Any<CancellationToken>());
        result.LessonId.Should().Be(_lesson.Id);
        result.IndexedAt.Should().Be(IndexedAt);
        result.Matches.Should().Equal(new LessonContentMatchResult(match.ChunkId, LessonContentSection.Summary, "Recap", 1, null, "summary-1", "Resistance", 0.6));
    }

    [Fact]
    public async Task Handle_ExplicitTopAndExclusion_PassedToSearch()
    {
        GivenIndex(chunkCount: 2);
        _chunkRepository.SearchAsync(Arg.Any<Guid>(), Arg.Any<float[]>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([]);

        var result = await _handler.Handle(new SearchLessonContentQuery(_lesson.Id, "q", 3, false), TestContext.Current.CancellationToken);

        result.Matches.Should().BeEmpty();
        await _chunkRepository.Received(1).SearchAsync(_lesson.Id, _queryVector, "text-embedding-3-small", 3, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotIndexed_ReturnsEmptyWithoutCallingAiService()
    {
        var result = await _handler.Handle(new SearchLessonContentQuery(_lesson.Id, "q", null), TestContext.Current.CancellationToken);

        result.IndexedAt.Should().BeNull();
        result.Matches.Should().BeEmpty();
        await _aiServiceClient.DidNotReceive().EmbedAsync(Arg.Any<AiEmbeddingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_IndexWithNoChunks_ReturnsEmptyWithoutCallingAiService()
    {
        GivenIndex(chunkCount: 0);

        var result = await _handler.Handle(new SearchLessonContentQuery(_lesson.Id, "q", null), TestContext.Current.CancellationToken);

        result.IndexedAt.Should().Be(IndexedAt);
        result.Matches.Should().BeEmpty();
        await _aiServiceClient.DidNotReceive().EmbedAsync(Arg.Any<AiEmbeddingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownLesson_ThrowsLessonNotFound()
    {
        var act = () => _handler.Handle(new SearchLessonContentQuery(Guid.NewGuid(), "q", null), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
    }

    [Fact]
    public async Task Handle_DraftLesson_ThrowsLessonNotFound()
    {
        var draft = new QuestionBuilder().Lesson;
        _lessonRepository.GetByIdAsync(draft.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), true).Returns(draft);

        var act = () => _handler.Handle(new SearchLessonContentQuery(draft.Id, "q", null), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _aiServiceClient.DidNotReceive().EmbedAsync(Arg.Any<AiEmbeddingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_QueryVectorWrongDimensions_ThrowsAiServiceUnavailable()
    {
        GivenIndex(chunkCount: 2);
        _aiServiceClient.EmbedAsync(Arg.Any<AiEmbeddingRequest>(), Arg.Any<CancellationToken>()).Returns(new AiEmbeddingResult("other", 3, [new float[3]], 1));

        var act = () => _handler.Handle(new SearchLessonContentQuery(_lesson.Id, "q", null), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AiServiceUnavailable);
        await _chunkRepository.DidNotReceive().SearchAsync(Arg.Any<Guid>(), Arg.Any<float[]>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    private void GivenIndex(int chunkCount)
    {
        var index = LessonContentIndex.Create(_lesson.Id, _lesson.UpdationDate, null, chunkCount, "text-embedding-3-small", IndexedAt);
        _indexRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<LessonContentIndex, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<LessonContentIndex>, IQueryable<LessonContentIndex>>?>(), Arg.Any<Func<IQueryable<LessonContentIndex>, IOrderedQueryable<LessonContentIndex>>?>(), true)
            .Returns(call => call.Arg<Expression<Func<LessonContentIndex, bool>>>().Compile()(index) ? index : null);
    }
}
