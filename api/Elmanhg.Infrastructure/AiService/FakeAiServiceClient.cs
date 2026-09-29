using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.ContentRetrieval;
using Microsoft.Extensions.Hosting;

namespace Elmanhg.Infrastructure.AiService;

public sealed class FakeAiServiceClient(IHostEnvironment hostEnvironment) : IAiServiceClient
{
    public const string FakeReply = "هذا رد تجريبي من المساعد.";
    public const string FakeModel = "fake";
    public const string FakePromptVersion = "fake";
    public const string FakeEmbeddingModel = "fake";

    public Task<AiChatReply> ChatAsync(AiChatRequest request, CancellationToken cancellationToken)
    {
        EnsureNotProduction();
        IReadOnlyList<string> citations = request.Sources.Count > 0 ? [request.Sources[0].Reference] : [];
        return Task.FromResult(new AiChatReply(FakeReply, FakeModel, FakePromptVersion, 0, 0, "end_turn", citations));
    }

    public Task<AiEmbeddingResult> EmbedAsync(AiEmbeddingRequest request, CancellationToken cancellationToken)
    {
        EnsureNotProduction();
        var embeddings = request.Texts
            .Select(x => FakeEmbeddingVectors.Create(x, LessonContentChunk.EmbeddingDimensions))
            .ToList();
        return Task.FromResult(new AiEmbeddingResult(FakeEmbeddingModel, LessonContentChunk.EmbeddingDimensions, embeddings, 0));
    }

    private void EnsureNotProduction()
    {
        if (hostEnvironment.IsProduction())
        {
            throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable);
        }
    }
}
