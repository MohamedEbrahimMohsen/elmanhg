using Core.DDD.Entities;

namespace Elmanhg.Domain.Sessions;

public sealed record AttemptsRecorded(Session Session, IReadOnlyList<Attempt> Attempts) : DomainEvent;
