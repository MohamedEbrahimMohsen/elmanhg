using Core.Errors;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Questions;

public partial class Question
{
    public void Update(QuestionType type, QuestionContent content, QuestionMetadata metadata, Lesson lesson, Guid updatedBy)
    {
        if (type != Type)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionTypeImmutable);
        }

        EnsureObjectiveInLesson(lesson, metadata.ObjectiveId);

        var contentChanged = !CurrentContent.IsEquivalentTo(content);
        var metadataChanged = Difficulty != metadata.Difficulty || ObjectiveId != metadata.ObjectiveId || !Tags.SequenceEqual(metadata.Tags);
        if (!contentChanged && !metadataChanged)
        {
            return;
        }

        ApplyMetadata(metadata);
        if (contentChanged)
        {
            ApplyContent(content);
            Version += 1;
            if (ValidationStatus == QuestionValidationStatus.Approved)
            {
                ValidationStatus = QuestionValidationStatus.Pending;
                ValidatedBy = null;
                ValidatedAt = null;
            }

            Revisions.Add(QuestionRevision.Create(this, updatedBy));
        }

        UpdatedBy = updatedBy;
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
