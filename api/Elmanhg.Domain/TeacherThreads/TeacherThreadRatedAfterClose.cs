using Core.DDD.Entities;

namespace Elmanhg.Domain.TeacherThreads;

public sealed record TeacherThreadRatedAfterClose(TeacherThread Thread, DateTimeOffset RatedAt) : DomainEvent;
