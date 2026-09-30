using Core.DDD.Entities;

namespace Elmanhg.Domain.TeacherThreads;

public sealed record TeacherThreadClosed(TeacherThread Thread) : DomainEvent;
