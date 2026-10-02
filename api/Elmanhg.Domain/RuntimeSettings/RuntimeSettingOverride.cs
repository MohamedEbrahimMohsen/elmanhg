using Core.DDD.Entities;

namespace Elmanhg.Domain.RuntimeSettings;

public class RuntimeSettingOverride : AuditEntity, IAuditedEntity
{
    public string Key { get; private set; } = default!;
    public string? Value { get; private set; }
    public uint Version { get; private set; }

    public bool IsOverridden => Value is not null;

    private RuntimeSettingOverride(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static RuntimeSettingOverride Create(string key, string value, Guid createdBy)
    {
        return new RuntimeSettingOverride(Guid.NewGuid(), createdBy) { Key = key, Value = value };
    }

    public void Override(string value, Guid updatedBy)
    {
        Value = value;
        UpdatedBy = updatedBy;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void Reset(Guid updatedBy)
    {
        Value = null;
        UpdatedBy = updatedBy;
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
