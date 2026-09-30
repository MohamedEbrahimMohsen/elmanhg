using Core.DDD.Repositories;
using Elmanhg.Domain.Sessions.Exams;
using Elmanhg.Domain.SharedKernel;

namespace Elmanhg.Domain.Questions;

public interface IQuestionRepository : IRepository<Question>
{
    Task<bool> AnyInLessonAsync(Guid lessonId, CancellationToken cancellationToken);
    Task<Dictionary<Guid, int>> CountByLessonAsync(IReadOnlyCollection<Guid> lessonIds, CancellationToken cancellationToken);
    Task<int> CountServableAsync(CancellationToken cancellationToken);
    Task<Dictionary<Guid, int>> CountServableByLessonAsync(IReadOnlyCollection<Guid> lessonIds, CancellationToken cancellationToken);
    Task<List<ServableQuestionCount>> CountServableByUnitAndTypeAsync(Guid subjectId, CancellationToken cancellationToken);
    Task<List<Guid>> GetServableIdsInLessonAsync(Guid lessonId, CancellationToken cancellationToken);
    Task<List<Question>> GetServableInLessonAsync(Guid lessonId, CancellationToken cancellationToken);
    Task<List<ExamCandidate>> GetServableExamCandidatesAsync(IReadOnlyCollection<Guid> unitIds, CancellationToken cancellationToken);
    Task<List<QuestionRevision>> GetRevisionsAsync(IReadOnlyCollection<Guid> questionIds, CancellationToken cancellationToken);
    Task<Dictionary<Guid, QuestionPlacement>> GetPlacementsAsync(IReadOnlyCollection<Guid> questionIds, CancellationToken cancellationToken);
    Task<List<QuestionInventoryCount>> CountInventoryAsync(Guid? subjectId, CancellationToken cancellationToken);
    Task<int> CountServableInSubjectAsync(Guid subjectId, CancellationToken cancellationToken);
    Task<QuestionDecisionStats> GetDecisionStatsAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, Guid? teacherId, CancellationToken cancellationToken);
    Task<List<TeacherDecisionCount>> CountDecisionsByTeacherAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, CancellationToken cancellationToken);
    Task<List<DailyTotal>> CountDecisionsByDayAsync(MetricsWindow window, Guid? subjectId, CancellationToken cancellationToken);
}
