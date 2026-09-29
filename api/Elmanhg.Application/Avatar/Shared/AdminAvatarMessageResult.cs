using Elmanhg.Domain.Avatar;

namespace Elmanhg.Application.Avatar.Shared;

public sealed record AdminAvatarMessageResult(Guid Id, int Position, AvatarMessageRole Role, string Text, DateTimeOffset CreatedAt, string? Model, string? PromptVersion, int? InputTokens, int? OutputTokens, decimal? CostUsd, string? StopReason, int? HistoryMessageCount, List<AvatarCitationResult> Citations, AvatarMessageContext? Context);
