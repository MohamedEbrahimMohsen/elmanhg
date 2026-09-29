using Elmanhg.Domain.ContentRetrieval;

namespace Elmanhg.Application.ContentRetrieval.Shared;

public sealed record LessonContentMatchResult(Guid ChunkId, LessonContentSection Section, string? SectionTitle, int Position, Guid? QuestionId, string Reference, string Content, double Score);
