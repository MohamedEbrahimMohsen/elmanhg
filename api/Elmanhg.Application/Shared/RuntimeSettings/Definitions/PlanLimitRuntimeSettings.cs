using Core.DDD.Models;
using Elmanhg.Application.Shared.Options;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Shared.RuntimeSettings.Definitions;

public sealed class PlanLimitRuntimeSettings(IOptions<SubscriptionsOptions> subscriptionsOptions) : IRuntimeSettingDefinitions
{
    public static readonly RuntimeSettingKey<int> FreeDailyQuizQuestions = new("plans.freeDailyQuizQuestions");
    public static readonly RuntimeSettingKey<int> FreeDailyAvatarMessages = new("plans.freeDailyAvatarMessages");
    public static readonly RuntimeSettingKey<int> FreeOpenLessonsPerUnit = new("plans.freeOpenLessonsPerUnit");
    public static readonly RuntimeSettingKey<int> BaseDailyAvatarMessages = new("plans.baseDailyAvatarMessages");
    public static readonly RuntimeSettingKey<int> AskTeacherMonthlyQuestions = new("plans.askTeacherMonthlyQuestions");

    public IReadOnlyList<RuntimeSettingDefinition> Definitions =>
    [
        RuntimeSettingDefinition.ForInteger(FreeDailyQuizQuestions, RuntimeSettingGroup.PlanLimits, subscriptionsOptions.Value.FreeDailyQuizQuestions, 0, 1000, new LocalizedText("أسئلة التدريب اليومية في الباقة المجانية", "Free plan: quiz questions per day"), new LocalizedText("عدد أسئلة التدريب التي يجيب عنها الطالب المجاني يوميًا.", "How many quiz questions a Free student can answer per day.")),
        RuntimeSettingDefinition.ForInteger(FreeDailyAvatarMessages, RuntimeSettingGroup.PlanLimits, subscriptionsOptions.Value.FreeDailyAvatarMessages, 0, 1000, new LocalizedText("رسائل المساعد اليومية في الباقة المجانية", "Free plan: assistant messages per day"), new LocalizedText("عدد رسائل المساعد للطالب المجاني يوميًا.", "Assistant messages a Free student can send per day.")),
        RuntimeSettingDefinition.ForInteger(FreeOpenLessonsPerUnit, RuntimeSettingGroup.PlanLimits, subscriptionsOptions.Value.FreeOpenLessonsPerUnit, 0, 100, new LocalizedText("الدروس المفتوحة لكل وحدة في الباقة المجانية", "Free plan: open lessons per unit"), new LocalizedText("عدد الدروس الأولى المتاحة في كل وحدة للطالب المجاني.", "How many of the first lessons in each unit a Free student can open.")),
        RuntimeSettingDefinition.ForInteger(BaseDailyAvatarMessages, RuntimeSettingGroup.PlanLimits, subscriptionsOptions.Value.BaseDailyAvatarMessages, 1, 10000, new LocalizedText("رسائل المساعد اليومية في الباقة الأساسية", "Base plan: assistant messages per day"), new LocalizedText("عدد رسائل المساعد لمشترك الباقة الأساسية يوميًا.", "Assistant messages a Base subscriber can send per day.")),
        RuntimeSettingDefinition.ForInteger(AskTeacherMonthlyQuestions, RuntimeSettingGroup.PlanLimits, subscriptionsOptions.Value.AskTeacherMonthlyQuestions, 1, 1000, new LocalizedText("أسئلة اسأل معلّم الشهرية", "Ask a Teacher: questions per month"), new LocalizedText("عدد الأسئلة الجديدة المسموح بها شهريًا مع إضافة اسأل معلّم.", "New questions allowed per month with the Ask a Teacher add-on.")),
    ];
}
