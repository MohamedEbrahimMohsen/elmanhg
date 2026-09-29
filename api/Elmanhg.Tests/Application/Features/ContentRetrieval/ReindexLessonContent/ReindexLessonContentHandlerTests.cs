using Core.Errors;
using Elmanhg.Application.ContentRetrieval.ReindexLessonContent;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Infrastructure.RichText;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.ContentRetrieval.ReindexLessonContent;

public sealed class ReindexLessonContentHandlerTests
{
    private const string Objective = "State the first law";
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ILessonContentChunkRepository _chunkRepository = Substitute.For<ILessonContentChunkRepository>();
    private readonly ILessonContentIndexRepository _indexRepository = Substitute.For<ILessonContentIndexRepository>();
    private readonly IAiServiceClient _aiServiceClient = Substitute.For<IAiServiceClient>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ContentRetrievalOptions _options = new() { ChunkMaxCharacters = 1500, EmbeddingBatchSize = 32 };
    private readonly QuestionBuilder _builder = new();
    private readonly List<LessonContentChunk> _existing = [];
    private readonly List<AiEmbeddingRequest> _embedRequests = [];
    private readonly Lesson _lesson;
    private List<LessonContentChunk>? _added;
    private LessonContentIndex? _created;
    private LessonContentIndex? _index;

    public ReindexLessonContentHandlerTests()
    {
        _lesson = Lesson.Create(_builder.Unit, "Ohm", 2, Guid.NewGuid());
        _timeProvider.GetUtcNow().Returns(Now);
        _existing.Add(LessonContentChunk.Create(_lesson.Id, new LessonContentChunkDraft(LessonContentSection.Summary, null, 1, null, null, "old"), new float[LessonContentChunk.EmbeddingDimensions], "fake", Now.AddDays(-1)));
        _lessonRepository.GetWithObjectivesAsync(_lesson.Id, true, Arg.Any<CancellationToken>()).Returns(_lesson);
        _chunkRepository.FindAsync(Arg.Any<Expression<Func<LessonContentChunk, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<LessonContentChunk>, IQueryable<LessonContentChunk>>?>(), Arg.Any<Func<IQueryable<LessonContentChunk>, IOrderedQueryable<LessonContentChunk>>?>(), false)
            .Returns(call => _existing.Where(call.Arg<Expression<Func<LessonContentChunk, bool>>>().Compile()).ToList());
        _indexRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<LessonContentIndex, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<LessonContentIndex>, IQueryable<LessonContentIndex>>?>(), Arg.Any<Func<IQueryable<LessonContentIndex>, IOrderedQueryable<LessonContentIndex>>?>(), false)
            .Returns(call => _index is not null && call.Arg<Expression<Func<LessonContentIndex, bool>>>().Compile()(_index) ? _index : null);
        _chunkRepository.AddRangeAsync(Arg.Do<List<LessonContentChunk>>(x => _added = x), Arg.Any<CancellationToken>());
        _indexRepository.AddAsync(Arg.Do<LessonContentIndex>(x => _created = x), Arg.Any<CancellationToken>());
        _aiServiceClient.EmbedAsync(Arg.Do<AiEmbeddingRequest>(_embedRequests.Add), Arg.Any<CancellationToken>())
            .Returns(call => new AiEmbeddingResult("fake", LessonContentChunk.EmbeddingDimensions, call.Arg<AiEmbeddingRequest>().Texts.Select(_ => new float[LessonContentChunk.EmbeddingDimensions]).ToList(), 0));
    }

    [Fact]
    public async Task Handle_PublishedLesson_EmbedsDocumentsReplacesChunksAndCreatesIndex()
    {
        var question = Publish("<h2>Law</h2><p>Current</p>", [Objective], [_builder.Approved().Build()])[0];

        await Handle();

        _embedRequests.Should().ContainSingle().Which.InputType.Should().Be(AiEmbeddingInputType.Document);
        _embedRequests[0].Texts.Should().Equal("Law\nCurrent", $"1. {Objective}", "2 + 2 = ?\nAdd the numbers.");
        _chunkRepository.Received(1).DeleteRange(Arg.Is<List<LessonContentChunk>>(x => x.SequenceEqual(_existing)));
        _added.Should().HaveCount(3);
        _created.Should().BeEquivalentTo(new { LessonId = _lesson.Id, SourceUpdatedAt = _lesson.UpdationDate, QuestionsUpdatedAt = (DateTimeOffset?)question.UpdationDate, ChunkCount = 3, EmbeddingModel = "fake", IndexedAt = Now });
        await _indexRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingIndex_MarksIndexedInsteadOfAdding()
    {
        Publish("<p>Current</p>", [Objective], []);
        _index = LessonContentIndex.Create(_lesson.Id, Now.AddDays(-2), Now.AddDays(-2), 9, "old", Now.AddDays(-2));

        await Handle();

        _index.Should().BeEquivalentTo(new { SourceUpdatedAt = _lesson.UpdationDate, QuestionsUpdatedAt = (DateTimeOffset?)null, ChunkCount = 2, EmbeddingModel = "fake", IndexedAt = Now });
        await _indexRepository.DidNotReceive().AddAsync(Arg.Any<LessonContentIndex>(), Arg.Any<CancellationToken>());
        await _indexRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MoreDraftsThanBatchSize_EmbedsInSequentialBatches()
    {
        _options.EmbeddingBatchSize = 2;
        Publish("<h2>A</h2><p>a</p><h2>B</h2><p>b</p>", [Objective], []);

        await Handle();

        _embedRequests.Select(x => x.Texts.Count).Should().Equal(2, 1);
        _added.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_NoContent_StoresEmptyIndexWithoutCallingAiService()
    {
        Publish(string.Empty, [], []);

        await Handle();

        _created.Should().BeEquivalentTo(new { ChunkCount = 0, EmbeddingModel = (string?)null, QuestionsUpdatedAt = (DateTimeOffset?)null });
        await _aiServiceClient.DidNotReceive().EmbedAsync(Arg.Any<AiEmbeddingRequest>(), Arg.Any<CancellationToken>());
        await _indexRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnpublishedLesson_DeletesChunksAndIndex()
    {
        _index = LessonContentIndex.Create(_lesson.Id, Now, null, 1, "fake", Now);

        await Handle();

        await ExpectRemovedAsync();
    }

    [Fact]
    public async Task Handle_MissingLesson_DeletesChunksAndIndex()
    {
        _index = LessonContentIndex.Create(_lesson.Id, Now, null, 1, "fake", Now);
        _lessonRepository.GetWithObjectivesAsync(_lesson.Id, true, Arg.Any<CancellationToken>()).Returns((Lesson?)null);

        await Handle();

        await ExpectRemovedAsync();
    }

    [Fact]
    public async Task Handle_AiServiceUnavailable_ThrowsAndDoesNotSave()
    {
        Publish("<p>Current</p>", [], []);
        _aiServiceClient.EmbedAsync(Arg.Any<AiEmbeddingRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable));

        var act = Handle;

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AiServiceUnavailable);
        _chunkRepository.DidNotReceive().DeleteRange(Arg.Any<List<LessonContentChunk>>());
        await _indexRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private List<Question> Publish(string explanation, IReadOnlyList<string> objectives, List<Question> questions)
    {
        _lesson.Update(_lesson.Name, explanation, string.Empty, null, objectives.Select(x => new LessonObjectiveContent(null, x)).ToList(), Guid.NewGuid());
        _lesson.Publish(Guid.NewGuid());
        _questionRepository.GetServableInLessonAsync(_lesson.Id, Arg.Any<CancellationToken>()).Returns(questions);
        return questions;
    }

    private async Task ExpectRemovedAsync()
    {
        _chunkRepository.Received(1).DeleteRange(Arg.Is<List<LessonContentChunk>>(x => x.SequenceEqual(_existing)));
        _indexRepository.Received(1).Delete(_index!);
        await _indexRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _aiServiceClient.DidNotReceive().EmbedAsync(Arg.Any<AiEmbeddingRequest>(), Arg.Any<CancellationToken>());
    }

    private Task Handle() => new ReindexLessonContentHandler(_lessonRepository, _questionRepository, _chunkRepository, _indexRepository, new RichTextExtractor(), _aiServiceClient, Options.Create(_options), _timeProvider).Handle(new ReindexLessonContentCommand(_lesson.Id), TestContext.Current.CancellationToken);
}
