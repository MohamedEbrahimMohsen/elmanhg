using Elmanhg.Domain.Sessions;
using System.Linq.Expressions;

namespace Elmanhg.Application.Progress.GetSessionHistory;

public static class GetSessionHistoryFilter
{
    public static Expression<Func<Session, bool>> Build(Guid studentId, SessionHistoryKind? kind)
    {
        var quizOnly = kind == SessionHistoryKind.Quiz;
        var examOnly = kind == SessionHistoryKind.Exam;

        return x => x.StudentId == studentId
            && !x.IsTestMode
            && (!quizOnly || x.Kind == SessionKind.Quiz)
            && (!examOnly || x.Kind != SessionKind.Quiz);
    }
}
