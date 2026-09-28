using Core.DDD.Entities;

namespace Elmanhg.Domain.Questions;

public sealed record QuestionApproved(Guid QuestionId, Guid LessonId) : DomainEvent;
