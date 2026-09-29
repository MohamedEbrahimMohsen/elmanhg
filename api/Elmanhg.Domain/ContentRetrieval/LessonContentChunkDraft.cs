namespace Elmanhg.Domain.ContentRetrieval;

public sealed record LessonContentChunkDraft(LessonContentSection Section, string? SectionTitle, int Position, Guid? QuestionId, int? QuestionVersion, string Content)
{
    public string EmbeddingText => SectionTitle is null ? Content : $"{SectionTitle}\n{Content}";
}
