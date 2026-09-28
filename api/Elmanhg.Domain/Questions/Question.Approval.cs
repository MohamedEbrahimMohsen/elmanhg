using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Teachers;

namespace Elmanhg.Domain.Questions;

public partial class Question
{
    public void Approve(TeacherSubject assignment)
    {
        if (ValidationStatus != QuestionValidationStatus.Pending)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionNotPending);
        }

        if (assignment.IsDeleted || assignment.SubjectId != SubjectId)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionValidatorNotAssigned);
        }

        var now = DateTimeOffset.UtcNow;
        ValidationStatus = QuestionValidationStatus.Approved;
        ValidatedBy = assignment.TeacherId;
        ValidatedAt = now;
        UpdatedBy = assignment.TeacherId;
        UpdationDate = now;
    }
}
