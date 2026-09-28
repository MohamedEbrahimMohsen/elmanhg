using Core.DDD.Repositories;

namespace Elmanhg.Domain.Questions;

public interface IQuestionRepository : IRepository<Question>
{
    Task<bool> AnyInLessonAsync(Guid lessonId, CancellationToken cancellationToken);
    Task<Dictionary<Guid, int>> CountByLessonAsync(IReadOnlyCollection<Guid> lessonIds, CancellationToken cancellationToken);
    Task<int> CountServableAsync(CancellationToken cancellationToken);
    Task<Dictionary<Guid, int>> CountServableByLessonAsync(IReadOnlyCollection<Guid> lessonIds, CancellationToken cancellationToken);
    Task<List<Guid>> GetServableIdsInLessonAsync(Guid lessonId, CancellationToken cancellationToken);
    Task<List<QuestionRevision>> GetRevisionsAsync(IReadOnlyCollection<Guid> questionIds, CancellationToken cancellationToken);
}
