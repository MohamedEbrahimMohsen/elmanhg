using Core.DDD.Entities;

namespace Elmanhg.Domain.Questions;

public sealed record QuestionReturnedToPending(Guid QuestionId, Guid LessonId) : DomainEvent;
