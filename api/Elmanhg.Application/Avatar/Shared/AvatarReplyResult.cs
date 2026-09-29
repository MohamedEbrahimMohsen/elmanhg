namespace Elmanhg.Application.Avatar.Shared;

public sealed record AvatarReplyResult(Guid ConversationId, string Reply, List<AvatarCitationResult> Citations, int DailyMessageLimit, int MessagesUsedToday, int MessagesRemainingToday, string Model, string PromptVersion);
