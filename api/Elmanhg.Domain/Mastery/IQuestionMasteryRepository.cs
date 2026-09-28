using Core.DDD.Repositories;

namespace Elmanhg.Domain.Mastery;

public interface IQuestionMasteryRepository : IRepository<QuestionMastery>
{
    Task<List<LessonMasteryCount>> GetLessonCountsAsync(Guid studentId, Guid? subjectId, CancellationToken cancellationToken);
}
