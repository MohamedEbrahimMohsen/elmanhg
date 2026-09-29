using Elmanhg.Application.ContentRetrieval.Shared;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.ContentRetrieval.ReindexLessonContent;

public sealed class ReindexLessonContentHandler(ILessonRepository lessonRepository, IQuestionRepository questionRepository, ILessonContentChunkRepository lessonContentChunkRepository, ILessonContentIndexRepository lessonContentIndexRepository, IRichTextExtractor richTextExtractor, IAiServiceClient aiServiceClient, IOptions<ContentRetrievalOptions> contentRetrievalOptions, TimeProvider timeProvider) : IRequestHandler<ReindexLessonContentCommand>
{
    public async Task Handle(ReindexLessonContentCommand request, CancellationToken cancellationToken)
    {
        var lesson = await lessonRepository.GetWithObjectivesAsync(request.LessonId, true, cancellationToken).ConfigureAwait(false);
        var index = await lessonContentIndexRepository.FirstOrDefaultAsync(x => x.LessonId == request.LessonId, cancellationToken).ConfigureAwait(false);
        var existing = await lessonContentChunkRepository.FindAsync(x => x.LessonId == request.LessonId, cancellationToken).ConfigureAwait(false);
        if (lesson is null || lesson.State != LessonState.Published)
        {
            lessonContentChunkRepository.DeleteRange(existing);
            if (index is not null)
            {
                lessonContentIndexRepository.Delete(index);
            }

            await lessonContentIndexRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var options = contentRetrievalOptions.Value;
        var questions = await questionRepository.GetServableInLessonAsync(lesson.Id, cancellationToken).ConfigureAwait(false);
        var drafts = LessonContentChunkPlanner.Plan(lesson, questions, richTextExtractor, options.ChunkMaxCharacters);
        DateTimeOffset? questionsUpdatedAt = questions.Count == 0 ? null : questions.Max(x => x.UpdationDate);
        var now = timeProvider.GetUtcNow();
        var (chunks, model) = await EmbedAsync(lesson.Id, drafts, options.EmbeddingBatchSize, now, cancellationToken).ConfigureAwait(false);

        lessonContentChunkRepository.DeleteRange(existing);
        await lessonContentChunkRepository.AddRangeAsync(chunks, cancellationToken).ConfigureAwait(false);
        if (index is null)
        {
            await lessonContentIndexRepository.AddAsync(LessonContentIndex.Create(lesson.Id, lesson.UpdationDate, questionsUpdatedAt, chunks.Count, model, now), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            index.MarkIndexed(lesson.UpdationDate, questionsUpdatedAt, chunks.Count, model, now);
        }

        await lessonContentIndexRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<(List<LessonContentChunk> Chunks, string? Model)> EmbedAsync(Guid lessonId, List<LessonContentChunkDraft> drafts, int batchSize, DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<LessonContentChunk> chunks = [];
        string? model = null;
        foreach (var batch in drafts.Chunk(batchSize))
        {
            var texts = batch
                .Select(x => x.EmbeddingText)
                .ToList();
            var result = await aiServiceClient.EmbedAsync(new AiEmbeddingRequest(AiEmbeddingInputType.Document, texts), cancellationToken).ConfigureAwait(false);
            chunks.AddRange(batch.Select((draft, position) => LessonContentChunk.Create(lessonId, draft, result.Embeddings[position], result.Model, now)));
            model ??= result.Model;
        }

        return (chunks, model);
    }
}
