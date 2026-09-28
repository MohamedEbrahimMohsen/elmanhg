using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Subjects;

public class Subject : AuditEntity, IAuditedEntity
{
    public string Name { get; private set; } = string.Empty;
    public int Order { get; private set; }

    private Subject(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static Subject Create(string name, int order, Guid createdBy)
    {
        if (order < 1)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ContentOrderInvalid);
        }

        return new Subject(Guid.NewGuid(), createdBy)
        {
            Name = name.Trim(),
            Order = order,
        };
    }

    public void Rename(string name, Guid updatedBy)
    {
        Name = name.Trim();
        UpdatedBy = updatedBy;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void MoveTo(int order, Guid updatedBy)
    {
        if (order < 1)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ContentOrderInvalid);
        }

        if (Order == order)
        {
            return;
        }

        Order = order;
        UpdatedBy = updatedBy;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void Delete(bool hasUnits, Guid deletedBy)
    {
        if (hasUnits)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SubjectHasUnits);
        }

        SoftDelete();
        UpdatedBy = deletedBy;
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
