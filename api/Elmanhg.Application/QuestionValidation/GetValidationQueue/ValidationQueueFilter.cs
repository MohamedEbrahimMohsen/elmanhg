using Elmanhg.Domain.Questions;
using System.Linq.Expressions;

namespace Elmanhg.Application.QuestionValidation.GetValidationQueue;

public static class ValidationQueueFilter
{
    public static Expression<Func<Question, bool>> Build(IReadOnlyCollection<Guid> subjectIds, IReadOnlyCollection<Guid>? unitLessonIds, GetValidationQueueQuery query, DateTimeOffset? submittedBefore)
    {
        var lessonId = query.LessonId;
        var type = query.Type;
        var difficulty = query.Difficulty;

        return x => subjectIds.Contains(x.SubjectId)
            && x.ValidationStatus == QuestionValidationStatus.Pending
            && x.RetiredAt == null
            && (unitLessonIds == null || unitLessonIds.Contains(x.LessonId))
            && (lessonId == null || x.LessonId == lessonId)
            && (type == null || x.Type == type)
            && (difficulty == null || x.Difficulty == difficulty)
            && (submittedBefore == null || x.SubmittedAt <= submittedBefore);
    }
}
