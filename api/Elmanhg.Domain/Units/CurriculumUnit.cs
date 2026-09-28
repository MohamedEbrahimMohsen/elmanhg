using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Subjects;

namespace Elmanhg.Domain.Units;

public class CurriculumUnit : AuditEntity, IAuditedEntity
{
    public Guid SubjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Order { get; private set; }

    private CurriculumUnit(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static CurriculumUnit Create(Subject subject, string name, int order, Guid createdBy)
    {
        if (order < 1)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ContentOrderInvalid);
        }

        return new CurriculumUnit(Guid.NewGuid(), createdBy)
        {
            SubjectId = subject.Id,
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

    public void Delete(Guid deletedBy)
    {
        SoftDelete();
        UpdatedBy = deletedBy;
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
