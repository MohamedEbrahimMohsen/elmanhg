using Core.Auditing.Entities;

namespace Elmanhg.Application.AuditLogs.Shared;

public static class AuditLogResultGenerator
{
    public static AuditLogResult Generate(AuditLog auditLog)
    {
        return new AuditLogResult(auditLog.Id, auditLog.Timestamp, auditLog.ActorUserId, auditLog.ActorUserName, auditLog.ActorRole, auditLog.Action, auditLog.ResourceType, auditLog.ResourceId, auditLog.Outcome, auditLog.ErrorCode, auditLog.TraceId, auditLog.Diff);
    }
}
