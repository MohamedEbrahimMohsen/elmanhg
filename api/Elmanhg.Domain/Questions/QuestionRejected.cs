using Core.DDD.Entities;

namespace Elmanhg.Domain.Questions;

public sealed record QuestionRejected(Guid QuestionId, Guid LessonId) : DomainEvent;
