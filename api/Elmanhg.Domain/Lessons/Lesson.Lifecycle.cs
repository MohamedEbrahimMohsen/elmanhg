using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Lessons;

public partial class Lesson
{
    public void Publish(Guid publishedBy)
    {
        if (State == LessonState.Published)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.LessonAlreadyPublished);
        }

        State = LessonState.Published;
        PublishedAt = DateTimeOffset.UtcNow;
        UpdatedBy = publishedBy;
        RaiseDomainEvent(new LessonPublished(Id, UnitId));
    }

    public void Unpublish(Guid updatedBy)
    {
        if (State == LessonState.Draft)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.LessonAlreadyDraft);
        }

        var wasPublished = State == LessonState.Published;
        State = LessonState.Draft;
        UpdatedBy = updatedBy;
        if (wasPublished)
        {
            RaiseDomainEvent(new LessonUnpublished(Id, UnitId));
        }
    }

    public void Archive(Guid updatedBy)
    {
        if (State == LessonState.Archived)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.LessonAlreadyArchived);
        }

        if (State == LessonState.Draft)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.LessonNotPublished);
        }

        State = LessonState.Archived;
        UpdatedBy = updatedBy;
        RaiseDomainEvent(new LessonArchived(Id, UnitId));
    }
}
