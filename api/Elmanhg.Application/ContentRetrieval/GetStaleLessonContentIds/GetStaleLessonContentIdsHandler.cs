using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.ContentRetrieval;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.ContentRetrieval.GetStaleLessonContentIds;

public sealed class GetStaleLessonContentIdsHandler(ILessonContentIndexRepository lessonContentIndexRepository, IOptions<ContentRetrievalOptions> contentRetrievalOptions) : IRequestHandler<GetStaleLessonContentIdsQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetStaleLessonContentIdsQuery request, CancellationToken cancellationToken)
    {
        return await lessonContentIndexRepository.GetStaleLessonIdsAsync(request.ExcludedIds, contentRetrievalOptions.Value.IndexSweepBatchSize, cancellationToken).ConfigureAwait(false);
    }
}
