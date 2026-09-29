using Core.DDD.Repositories;

namespace Elmanhg.Domain.ContentRetrieval;

public interface ILessonContentChunkRepository : IRepository<LessonContentChunk>
{
    Task<List<LessonContentMatch>> SearchAsync(Guid lessonId, float[] queryEmbedding, string embeddingModel, int top, bool includeQuestionExplanations, CancellationToken cancellationToken);
}
