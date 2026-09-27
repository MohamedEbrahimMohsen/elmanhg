using Core.Auditing.Entities;
using System.Linq.Expressions;

namespace Elmanhg.Application.AuditLogs.GetAuditLogs;

public static class GetAuditLogsFilter
{
    public static Expression<Func<AuditLog, bool>> Build(GetAuditLogsQuery query)
    {
        var actor = string.IsNullOrWhiteSpace(query.Actor) ? null : query.Actor.Trim().ToLowerInvariant();
        var resourceType = string.IsNullOrWhiteSpace(query.ResourceType) ? null : query.ResourceType.Trim();
        var from = query.From?.ToUniversalTime();
        var to = query.To?.ToUniversalTime();

        return x => (actor == null || (x.ActorUserName != null && x.ActorUserName.ToLower().Contains(actor)))
            && (resourceType == null || x.ResourceType == resourceType)
            && (from == null || x.Timestamp >= from)
            && (to == null || x.Timestamp < to);
    }
}
