using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.Shared;

public sealed record EntitlementResult(PlanTier Tier, bool HasAskTeacher, bool CanTakeExams, int? DailyQuizQuestionLimit, int DailyAvatarMessageLimit, int? OpenLessonsPerUnit, int MonthlyAskTeacherQuestionLimit, List<SubscriptionResult> Subscriptions);
