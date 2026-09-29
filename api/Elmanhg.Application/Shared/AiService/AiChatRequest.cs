namespace Elmanhg.Application.Shared.AiService;

public sealed record AiChatRequest(AiContextBundle Context, IReadOnlyList<AiChatMessage> History, string Message, IReadOnlyList<AiChatSource> Sources);
