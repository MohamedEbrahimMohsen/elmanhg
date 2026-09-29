namespace Elmanhg.Application.Shared.AiService;

public sealed record AiEmbeddingResult(string Model, int Dimensions, IReadOnlyList<float[]> Embeddings, int InputTokens);
