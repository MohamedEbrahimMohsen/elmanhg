using Core.DDD.Entities;

namespace Elmanhg.Domain.Lessons;

public sealed record LessonUnpublished(Guid LessonId, Guid UnitId) : DomainEvent;
