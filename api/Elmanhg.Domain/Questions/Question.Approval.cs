using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Teachers;

namespace Elmanhg.Domain.Questions;

public partial class Question
{
    public void Approve(TeacherSubject assignment)
    {
        EnsureValidatorCanDecide(assignment);

        var now = DateTimeOffset.UtcNow;
        ValidationStatus = QuestionValidationStatus.Approved;
        ValidatedBy = assignment.TeacherId;
        ValidatedAt = now;
        UpdatedBy = assignment.TeacherId;
        UpdationDate = now;
        RaiseDomainEvent(new QuestionApproved(Id, LessonId));
    }

    public void Reject(TeacherSubject assignment, string reason)
    {
        EnsureValidatorCanDecide(assignment);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionRejectionReasonRequired);
        }

        var now = DateTimeOffset.UtcNow;
        ValidationStatus = QuestionValidationStatus.Rejected;
        RejectionReason = reason.Trim();
        ValidatedBy = assignment.TeacherId;
        ValidatedAt = now;
        UpdatedBy = assignment.TeacherId;
        UpdationDate = now;
        RaiseDomainEvent(new QuestionRejected(Id, LessonId));
    }

    private void EnsureValidatorCanDecide(TeacherSubject assignment)
    {
        EnsureNotRetired();
        if (ValidationStatus != QuestionValidationStatus.Pending)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionNotPending);
        }

        if (assignment.IsDeleted || assignment.SubjectId != SubjectId)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionValidatorNotAssigned);
        }
    }
}
