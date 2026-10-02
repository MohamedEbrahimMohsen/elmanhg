using Core.DDD.Models;
using Elmanhg.Application.Shared.Options;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Shared.RuntimeSettings.Definitions;

public sealed class FeatureFlagRuntimeSettings(IOptions<ExamsOptions> examsOptions) : IRuntimeSettingDefinitions
{
    public static readonly RuntimeSettingKey<bool> ExamsRequireAllLessonsOpened = new("features.examsRequireAllLessonsOpened");

    public IReadOnlyList<RuntimeSettingDefinition> Definitions =>
    [
        RuntimeSettingDefinition.ForBoolean(ExamsRequireAllLessonsOpened, RuntimeSettingGroup.Features, examsOptions.Value.RequireAllLessonsOpened, new LocalizedText("اشتراط فتح كل دروس الوحدة قبل امتحانها", "Require opening every lesson before a unit exam"), new LocalizedText("عند التفعيل، لا يبدأ الطالب امتحان الوحدة حتى يفتح كل دروسها.", "When on, a student cannot start a unit exam until they have opened every lesson in the unit.")),
    ];
}
