using Core.DDD.Entities;

namespace Elmanhg.Domain.Lessons;

public sealed record LessonArchived(Guid LessonId, Guid UnitId) : DomainEvent;
