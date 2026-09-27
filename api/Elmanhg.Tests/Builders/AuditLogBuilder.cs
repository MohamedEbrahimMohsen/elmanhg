using Core.Auditing.Entities;

namespace Elmanhg.Tests.Builders;

public sealed class AuditLogBuilder
{
    private DateTimeOffset _timestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private string? _actorUserName = "admin@elmanhg.test";
    private string _resourceType = "Teacher";
    private string? _diff;

    public AuditLogBuilder WithActorUserName(string? actorUserName)
    {
        _actorUserName = actorUserName;
        return this;
    }

    public AuditLogBuilder WithResourceType(string resourceType)
    {
        _resourceType = resourceType;
        return this;
    }

    public AuditLogBuilder WithTimestamp(DateTimeOffset timestamp)
    {
        _timestamp = timestamp;
        return this;
    }

    public AuditLogBuilder WithDiff(string? diff)
    {
        _diff = diff;
        return this;
    }

    public AuditLog Build()
    {
        return AuditLog.Create(_timestamp, Guid.NewGuid(), _actorUserName, "Admin", "Teacher.AssignSubject", _resourceType, Guid.NewGuid(), "Success", null, "trace-1", _diff);
    }
}
