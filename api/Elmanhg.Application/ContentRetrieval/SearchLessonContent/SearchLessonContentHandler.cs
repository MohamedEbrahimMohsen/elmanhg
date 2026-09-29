using Core.Errors;
using Elmanhg.Application.ContentRetrieval.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Domain.Lessons;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.ContentRetrieval.SearchLessonContent;

public sealed class SearchLessonContentHandler(ILessonRepository lessonRepository, ILessonContentIndexRepository lessonContentIndexRepository, ILessonContentChunkRepository lessonContentChunkRepository, IAiServiceClient aiServiceClient, IOptions<ContentRetrievalOptions> contentRetrievalOptions) : IRequestHandler<SearchLessonContentQuery, LessonContentSearchResult>
{
    public async Task<LessonContentSearchResult> Handle(SearchLessonContentQuery request, CancellationToken cancellationToken)
    {
        var lesson = await lessonRepository.GetByIdAsync(request.LessonId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (lesson is null || lesson.State != LessonState.Published)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        var index = await lessonContentIndexRepository.FirstOrDefaultAsync(x => x.LessonId == lesson.Id, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (index is null || index.ChunkCount == 0)
        {
            return new LessonContentSearchResult(lesson.Id, index?.IndexedAt, []);
        }

        var embedding = await aiServiceClient.EmbedAsync(new AiEmbeddingRequest(AiEmbeddingInputType.Query, [request.Query.Trim()]), cancellationToken).ConfigureAwait(false);
        var queryVector = embedding.Embeddings[0];
        if (queryVector.Length != LessonContentChunk.EmbeddingDimensions)
        {
            throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable);
        }

        var matches = await lessonContentChunkRepository.SearchAsync(lesson.Id, queryVector, embedding.Model, request.Top ?? contentRetrievalOptions.Value.DefaultTopK, request.IncludeQuestionExplanations, cancellationToken).ConfigureAwait(false);
        return new LessonContentSearchResult(lesson.Id, index.IndexedAt, matches
            .Select(LessonContentMatchResultGenerator.Generate)
            .ToList());
    }
}
