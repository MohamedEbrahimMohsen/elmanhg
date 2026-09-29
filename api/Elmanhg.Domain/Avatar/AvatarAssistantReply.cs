namespace Elmanhg.Domain.Avatar;

public sealed record AvatarAssistantReply(string Text, string Model, string PromptVersion, int InputTokens, int OutputTokens, decimal CostUsd, string? StopReason, int HistoryMessageCount, string Context, string Citations);
