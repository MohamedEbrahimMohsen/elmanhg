namespace Core.DDD.Entities;

public abstract class AuditEntity(Guid id, Guid? createdBy = null) : Entity(id)
{
    public Guid? CreatedBy { get; set; } = createdBy;
    public DateTimeOffset CreationDate { get; set; } = DateTimeOffset.UtcNow;
    public Guid? UpdatedBy { get; set; } = createdBy;
    public DateTimeOffset UpdationDate { get; set; } = DateTimeOffset.UtcNow;
}

public interface IAuditEntity : IEntity
{
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreationDate { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset UpdationDate { get; set; }
}