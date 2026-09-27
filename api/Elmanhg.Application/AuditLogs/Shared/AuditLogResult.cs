namespace Elmanhg.Application.AuditLogs.Shared;

public sealed record AuditLogResult(Guid Id, DateTimeOffset Timestamp, Guid? ActorUserId, string? ActorUserName, string? ActorRole, string Action, string ResourceType, Guid? ResourceId, string Outcome, string? ErrorCode, string? TraceId, string? Diff);
