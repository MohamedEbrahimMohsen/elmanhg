using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Teachers;

namespace Elmanhg.Domain.Questions;

public partial class Question
{
    public void Approve(TeacherSubject assignment, int reviewedVersion, QuestionDifficulty? difficulty = null)
    {
        EnsureValidatorCanDecide(assignment, reviewedVersion);

        var now = DateTimeOffset.UtcNow;
        QuestionDifficulty? changedFrom = null;
        if (difficulty is not null && difficulty.Value != Difficulty)
        {
            changedFrom = Difficulty;
            Difficulty = difficulty.Value;
        }

        ValidationStatus = QuestionValidationStatus.Approved;
        ValidatedBy = assignment.TeacherId;
        ValidatedAt = now;
        UpdatedBy = assignment.TeacherId;
        UpdationDate = now;
        Decisions.Add(QuestionDecision.Create(Id, Version, QuestionDecisionOutcome.Approved, null, Difficulty, changedFrom, assignment.TeacherId, now));
        RaiseDomainEvent(new QuestionApproved(Id, LessonId));
    }

    public void Reject(TeacherSubject assignment, int reviewedVersion, string reason)
    {
        EnsureValidatorCanDecide(assignment, reviewedVersion);
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
        Decisions.Add(QuestionDecision.Create(Id, Version, QuestionDecisionOutcome.Rejected, RejectionReason, Difficulty, null, assignment.TeacherId, now));
        RaiseDomainEvent(new QuestionRejected(Id, LessonId));
    }

    private void EnsureValidatorCanDecide(TeacherSubject assignment, int reviewedVersion)
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

        if (reviewedVersion != Version)
        {
            throw new ConflictCoreException(ErrorCodes.QuestionVersionChanged);
        }
    }
}
