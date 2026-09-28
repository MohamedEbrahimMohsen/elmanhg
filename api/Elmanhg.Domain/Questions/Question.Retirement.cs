using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Questions;

public partial class Question
{
    public void Retire(Guid retiredBy)
    {
        if (RetiredAt is not null)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionAlreadyRetired);
        }

        var now = DateTimeOffset.UtcNow;
        RetiredAt = now;
        UpdatedBy = retiredBy;
        UpdationDate = now;
        RaiseDomainEvent(new QuestionRetired(Id, LessonId));
    }

    private void EnsureNotRetired()
    {
        if (RetiredAt is not null)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionRetired);
        }
    }
}
