using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Pgvector;

namespace Elmanhg.Domain.ContentRetrieval;

public class LessonContentChunk : Entity
{
    // Width of the vector column; changing it is a schema migration and a full re-embed, not a tunable.
    public const int EmbeddingDimensions = 1536;
    // Column width of the derived heading copy; longer headings are truncated by the chunker, never rejected.
    public const int SectionTitleMaxLength = 200;
    // Column width for provider model ids such as "text-embedding-3-small"; a schema invariant.
    public const int EmbeddingModelMaxLength = 200;

    public Guid LessonId { get; private set; }
    public LessonContentSection Section { get; private set; }
    public string? SectionTitle { get; private set; }
    public int Position { get; private set; }
    public Guid? QuestionId { get; private set; }
    public int? QuestionVersion { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public Vector Embedding { get; private set; } = default!;
    public string EmbeddingModel { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private LessonContentChunk(Guid id) : base(id) { }

    public static LessonContentChunk Create(Guid lessonId, LessonContentChunkDraft draft, float[] embedding, string embeddingModel, DateTimeOffset createdAt)
    {
        if (embedding.Length != EmbeddingDimensions)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ContentEmbeddingDimensionsInvalid);
        }

        return new LessonContentChunk(Guid.NewGuid())
        {
            LessonId = lessonId,
            Section = draft.Section,
            SectionTitle = draft.SectionTitle,
            Position = draft.Position,
            QuestionId = draft.QuestionId,
            QuestionVersion = draft.QuestionVersion,
            Content = draft.Content,
            Embedding = new Vector(embedding),
            EmbeddingModel = embeddingModel,
            CreatedAt = createdAt,
        };
    }
}
