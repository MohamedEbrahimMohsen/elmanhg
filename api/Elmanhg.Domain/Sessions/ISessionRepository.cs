using Core.DDD.Repositories;
using Elmanhg.Domain.Sessions.Selection;
using Elmanhg.Domain.SharedKernel;

namespace Elmanhg.Domain.Sessions;

public interface ISessionRepository : IRepository<Session>
{
    Task<List<QuestionAttemptSummary>> GetAttemptSummariesAsync(Guid studentId, IReadOnlyCollection<Guid> questionIds, decimal correctThreshold, CancellationToken cancellationToken);
    Task<List<DateOnly>> GetQuizActivityDaysAsync(Guid studentId, string timeZone, DateTimeOffset since, CancellationToken cancellationToken);
    Task<int> CountQuizAttemptsOnDayAsync(Guid studentId, string timeZone, DateOnly day, CancellationToken cancellationToken);
    Task<List<ExamBestScore>> GetBestExamScoresAsync(Guid studentId, CancellationToken cancellationToken);
    Task<List<ExamAttemptSummary>> GetExamAttemptsAsync(Guid studentId, SessionKind kind, string scopeKey, CancellationToken cancellationToken);
    Task<Attempt?> GetStudentAttemptAsync(Guid attemptId, Guid studentId, CancellationToken cancellationToken);
    Task<List<DailyTotal>> CountAttemptsByDayAsync(MetricsWindow window, Guid? subjectId, CancellationToken cancellationToken);
    Task<List<LessonAttemptOutcome>> GetAttemptOutcomesByLessonAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, decimal correctThreshold, CancellationToken cancellationToken);
}
