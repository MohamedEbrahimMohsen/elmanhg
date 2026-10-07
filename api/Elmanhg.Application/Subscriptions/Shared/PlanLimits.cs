using Core.Settings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;

namespace Elmanhg.Application.Subscriptions.Shared;

public sealed record PlanLimits(int FreeDailyQuizQuestions, int FreeDailyAvatarMessages, int FreeOpenLessonsPerUnit, int BaseDailyAvatarMessages, int AskTeacherMonthlyQuestions)
{
    public static PlanLimits From(RuntimeSettingValues values) => new(values.Get(PlanLimitRuntimeSettings.FreeDailyQuizQuestions), values.Get(PlanLimitRuntimeSettings.FreeDailyAvatarMessages), values.Get(PlanLimitRuntimeSettings.FreeOpenLessonsPerUnit), values.Get(PlanLimitRuntimeSettings.BaseDailyAvatarMessages), values.Get(PlanLimitRuntimeSettings.AskTeacherMonthlyQuestions));
}
