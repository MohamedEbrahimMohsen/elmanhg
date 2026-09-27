namespace Core.Auditing;

public interface IAuditChangeCollector
{
    IReadOnlyList<AuditEntityChange> Changes { get; }

    void Record(IReadOnlyList<AuditEntityChange> changes);
}
