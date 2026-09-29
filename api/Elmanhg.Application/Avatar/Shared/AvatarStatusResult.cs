using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Avatar.Shared;

public sealed record AvatarStatusResult(bool ExamInProgress, PlanTier Tier, int DailyMessageLimit, int MessagesUsedToday, int MessagesRemainingToday, int MessageMaxLength, int MaxHistoryMessages);
