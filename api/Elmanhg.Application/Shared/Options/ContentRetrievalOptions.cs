using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class ContentRetrievalOptions
{
    public const string SectionName = "ContentRetrieval";

    public bool IndexSweepEnabled { get; set; } = true;

    [Range(5, 86400)]
    public int IndexSweepIntervalSeconds { get; set; } = 30;

    [Range(1, 500)]
    public int IndexSweepBatchSize { get; set; } = 20;

    // 6000 plus a 200-character section title stays under the AI service's 8000-character text limit.
    [Range(200, 6000)]
    public int ChunkMaxCharacters { get; set; } = 1500;

    // At most ELMANHG_AI_EMBEDDING_MAX_TEXTS (64 by default) texts per embeddings call.
    [Range(1, 64)]
    public int EmbeddingBatchSize { get; set; } = 32;

    [Range(1, 50)]
    public int DefaultTopK { get; set; } = 5;

    [Range(1, 50)]
    public int MaxTopK { get; set; } = 20;

    [Range(1, 4000)]
    public int QueryMaxLength { get; set; } = 2000;
}
