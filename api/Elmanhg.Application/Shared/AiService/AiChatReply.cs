namespace Elmanhg.Application.Shared.AiService;

public sealed record AiChatReply(string Reply, string Model, string PromptVersion, int InputTokens, int OutputTokens, string? StopReason);
