using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Lessons;

public class LessonObjective : AuditEntity, IAuditedEntity
{
    public Guid LessonId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public int Order { get; private set; }

    private LessonObjective(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static LessonObjective Create(Guid lessonId, string text, int order, Guid createdBy)
    {
        if (order < 1)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ContentOrderInvalid);
        }

        return new LessonObjective(Guid.NewGuid(), createdBy)
        {
            LessonId = lessonId,
            Text = text.Trim(),
            Order = order,
        };
    }

    public void Update(string text, int order, Guid updatedBy)
    {
        if (order < 1)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ContentOrderInvalid);
        }

        var trimmed = text.Trim();
        if (Text == trimmed && Order == order)
        {
            return;
        }

        Text = trimmed;
        Order = order;
        UpdatedBy = updatedBy;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void Delete(Guid deletedBy)
    {
        var now = DateTimeOffset.UtcNow;
        SoftDelete(now);
        UpdatedBy = deletedBy;
        UpdationDate = now;
    }
}
