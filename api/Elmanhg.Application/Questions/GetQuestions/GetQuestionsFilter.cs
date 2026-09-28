using Elmanhg.Domain.Questions;
using System.Linq.Expressions;

namespace Elmanhg.Application.Questions.GetQuestions;

public static class GetQuestionsFilter
{
    public static Expression<Func<Question, bool>> Build(GetQuestionsQuery query)
    {
        var reason = string.IsNullOrWhiteSpace(query.RejectionReason) ? null : query.RejectionReason.Trim().ToLowerInvariant();
        var status = query.Status;
        var type = query.Type;
        var subjectId = query.SubjectId;
        var lessonId = query.LessonId;
        var teacherId = query.TeacherId;
        var minVersion = query.MinVersion;

        return x => (status == null || x.ValidationStatus == status)
            && (type == null || x.Type == type)
            && (subjectId == null || x.SubjectId == subjectId)
            && (lessonId == null || x.LessonId == lessonId)
            && (teacherId == null || x.ValidatedBy == teacherId)
            && (minVersion == null || x.Version >= minVersion)
            && (reason == null || (x.RejectionReason != null && x.RejectionReason.ToLower().Contains(reason)));
    }
}
