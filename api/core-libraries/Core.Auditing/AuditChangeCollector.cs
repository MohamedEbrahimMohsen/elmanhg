namespace Core.Auditing;

public sealed class AuditChangeCollector : IAuditChangeCollector
{
    private readonly List<AuditEntityChange> _changes = [];

    public IReadOnlyList<AuditEntityChange> Changes => _changes;

    public void Record(IReadOnlyList<AuditEntityChange> changes) => _changes.AddRange(changes);
}
