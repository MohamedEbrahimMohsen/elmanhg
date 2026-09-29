namespace Elmanhg.Application.Shared.AiService;

public sealed record AiEmbeddingRequest(AiEmbeddingInputType InputType, IReadOnlyList<string> Texts);
