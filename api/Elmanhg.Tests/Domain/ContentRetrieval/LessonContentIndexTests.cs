using Elmanhg.Domain.ContentRetrieval;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.ContentRetrieval;

public sealed class LessonContentIndexTests
{
    private static readonly DateTimeOffset SourceUpdatedAt = new(2026, 9, 29, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset QuestionsUpdatedAt = new(2026, 9, 29, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset IndexedAt = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_SetsStampsCountAndModel()
    {
        var lessonId = Guid.NewGuid();

        var index = LessonContentIndex.Create(lessonId, SourceUpdatedAt, QuestionsUpdatedAt, 4, "fake", IndexedAt);

        index.Id.Should().NotBeEmpty();
        index.LessonId.Should().Be(lessonId);
        index.SourceUpdatedAt.Should().Be(SourceUpdatedAt);
        index.QuestionsUpdatedAt.Should().Be(QuestionsUpdatedAt);
        index.ChunkCount.Should().Be(4);
        index.EmbeddingModel.Should().Be("fake");
        index.IndexedAt.Should().Be(IndexedAt);
    }

    [Fact]
    public void MarkIndexed_ReplacesStampsCountAndModel()
    {
        var lessonId = Guid.NewGuid();
        var index = LessonContentIndex.Create(lessonId, SourceUpdatedAt, QuestionsUpdatedAt, 4, "fake", IndexedAt);

        index.MarkIndexed(SourceUpdatedAt.AddHours(1), null, 0, null, IndexedAt.AddHours(1));

        index.LessonId.Should().Be(lessonId);
        index.SourceUpdatedAt.Should().Be(SourceUpdatedAt.AddHours(1));
        index.QuestionsUpdatedAt.Should().BeNull();
        index.ChunkCount.Should().Be(0);
        index.EmbeddingModel.Should().BeNull();
        index.IndexedAt.Should().Be(IndexedAt.AddHours(1));
    }
}
