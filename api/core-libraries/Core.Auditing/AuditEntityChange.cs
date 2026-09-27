namespace Core.Auditing;

public sealed record AuditEntityChange(string EntityType, Guid EntityId, AuditChangeKind Change, IReadOnlyDictionary<string, AuditValueChange> Properties);
