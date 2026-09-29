namespace Elmanhg.Application.Shared.AiService;

public sealed record AiTranscriptionRequest(byte[] Audio, string ContentType, string Language, int DurationSeconds);
