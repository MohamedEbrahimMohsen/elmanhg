namespace Elmanhg.Domain.ContentRetrieval;

public sealed record LessonContentMatch(Guid ChunkId, LessonContentSection Section, string? SectionTitle, int Position, Guid? QuestionId, string Content, double Distance);
