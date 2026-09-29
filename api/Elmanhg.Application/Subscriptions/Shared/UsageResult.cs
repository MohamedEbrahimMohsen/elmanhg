using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.Shared;

public sealed record UsageResult(PlanTier Tier, bool HasAskTeacher, int? DailyQuizQuestionLimit, int QuizQuestionsUsedToday, int? QuizQuestionsRemainingToday, int DailyAvatarMessageLimit);
