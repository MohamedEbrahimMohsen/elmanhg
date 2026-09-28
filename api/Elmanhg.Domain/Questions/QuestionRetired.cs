using Core.DDD.Entities;

namespace Elmanhg.Domain.Questions;

public sealed record QuestionRetired(Guid QuestionId, Guid LessonId) : DomainEvent;
