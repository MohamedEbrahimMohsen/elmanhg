using Core.DDD.Entities;

namespace Elmanhg.Domain.Subjects;

public class Subject : AuditEntity
{
    public string Name { get; private set; } = string.Empty;

    private Subject(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static Subject Create(string name, Guid createdBy)
    {
        return new Subject(Guid.NewGuid(), createdBy)
        {
            Name = name,
        };
    }
}
