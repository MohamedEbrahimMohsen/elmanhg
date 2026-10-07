using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.ContentRetrieval;

public class LessonContentChunkRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<LessonContentChunk>(context, currentUser, timeProvider), ILessonContentChunkRepository
{
    public async Task<List<LessonContentMatch>> SearchAsync(Guid lessonId, float[] queryEmbedding, string embeddingModel, int top, bool includeQuestionExplanations, CancellationToken cancellationToken)
    {
        var vector = new Vector(queryEmbedding);
        var servable = _context.Set<Question>().WhereServable(_context.Set<Lesson>());
        return await _dbSet
            .Where(x => x.LessonId == lessonId && x.EmbeddingModel == embeddingModel)
            .Where(x => x.QuestionId == null || (includeQuestionExplanations && servable.Any(question => question.Id == x.QuestionId && question.Version == x.QuestionVersion)))
            .OrderBy(x => x.Embedding.CosineDistance(vector))
            .ThenBy(x => x.Id)
            .Take(top)
            .Select(x => new LessonContentMatch(x.Id, x.Section, x.SectionTitle, x.Position, x.QuestionId, x.Content, x.Embedding.CosineDistance(vector)))
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
