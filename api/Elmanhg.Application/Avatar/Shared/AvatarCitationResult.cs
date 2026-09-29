using Elmanhg.Domain.ContentRetrieval;

namespace Elmanhg.Application.Avatar.Shared;

public sealed record AvatarCitationResult(string Reference, LessonContentSection Section, string? SectionTitle, Guid LessonId, Guid? QuestionId);
