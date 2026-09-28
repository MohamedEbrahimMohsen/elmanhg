using Core.DDD.Entities;

namespace Elmanhg.Domain.Lessons;

public sealed record LessonPublished(Guid LessonId, Guid UnitId) : DomainEvent;
