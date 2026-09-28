using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Questions.GetServableQuestionCount;

public sealed class GetServableQuestionCountHandler(IQuestionRepository questionRepository, IMemoryCache memoryCache, IOptions<ContentOptions> contentOptions) : IRequestHandler<GetServableQuestionCountQuery, ServableQuestionCountResult>
{
    public async Task<ServableQuestionCountResult> Handle(GetServableQuestionCountQuery request, CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(ServableQuestionCountCache.Key, out int cached))
        {
            return new ServableQuestionCountResult(cached);
        }

        var count = await questionRepository.CountServableAsync(cancellationToken).ConfigureAwait(false);
        memoryCache.Set(ServableQuestionCountCache.Key, count, TimeSpan.FromSeconds(contentOptions.Value.ServableCountCacheSeconds));
        return new ServableQuestionCountResult(count);
    }
}
