using Core.DDD.Entities;

namespace Elmanhg.Domain.ContentRetrieval;

public class LessonContentIndex : Entity
{
    public Guid LessonId { get; private set; }
    public DateTimeOffset SourceUpdatedAt { get; private set; }
    public DateTimeOffset? QuestionsUpdatedAt { get; private set; }
    public int ChunkCount { get; private set; }
    public string? EmbeddingModel { get; private set; }
    public DateTimeOffset IndexedAt { get; private set; }

    private LessonContentIndex(Guid id) : base(id) { }

    public static LessonContentIndex Create(Guid lessonId, DateTimeOffset sourceUpdatedAt, DateTimeOffset? questionsUpdatedAt, int chunkCount, string? embeddingModel, DateTimeOffset indexedAt)
    {
        return new LessonContentIndex(Guid.NewGuid())
        {
            LessonId = lessonId,
            SourceUpdatedAt = sourceUpdatedAt,
            QuestionsUpdatedAt = questionsUpdatedAt,
            ChunkCount = chunkCount,
            EmbeddingModel = embeddingModel,
            IndexedAt = indexedAt,
        };
    }

    public void MarkIndexed(DateTimeOffset sourceUpdatedAt, DateTimeOffset? questionsUpdatedAt, int chunkCount, string? embeddingModel, DateTimeOffset indexedAt)
    {
        SourceUpdatedAt = sourceUpdatedAt;
        QuestionsUpdatedAt = questionsUpdatedAt;
        ChunkCount = chunkCount;
        EmbeddingModel = embeddingModel;
        IndexedAt = indexedAt;
    }
}
