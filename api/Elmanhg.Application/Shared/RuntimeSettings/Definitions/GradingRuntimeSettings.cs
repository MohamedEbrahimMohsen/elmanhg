using Core.DDD.Models;
using Elmanhg.Application.Shared.Options;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Shared.RuntimeSettings.Definitions;

public sealed class GradingRuntimeSettings(IOptions<EssayGradingOptions> essayGradingOptions, IOptions<MathStepGradingOptions> mathStepGradingOptions) : IRuntimeSettingDefinitions
{
    public static readonly RuntimeSettingKey<decimal> EssayReviewConfidenceThreshold = new("grading.essayReviewConfidenceThreshold");
    public static readonly RuntimeSettingKey<decimal> MathStepReviewConfidenceThreshold = new("grading.mathStepReviewConfidenceThreshold");

    private static readonly LocalizedText ThresholdDescription = new("التصحيح بثقة أقل من هذه القيمة يذهب لمراجعة المعلّم (من 0 إلى 1).", "Grades with a confidence below this go to teacher review (0 to 1).");

    public IReadOnlyList<RuntimeSettingDefinition> Definitions =>
    [
        RuntimeSettingDefinition.ForDecimal(EssayReviewConfidenceThreshold, RuntimeSettingGroup.Grading, essayGradingOptions.Value.ReviewConfidenceThreshold, 0m, 1m, new LocalizedText("حد الثقة لتصحيح المقالات", "Essay grading: review threshold"), ThresholdDescription),
        RuntimeSettingDefinition.ForDecimal(MathStepReviewConfidenceThreshold, RuntimeSettingGroup.Grading, mathStepGradingOptions.Value.ReviewConfidenceThreshold, 0m, 1m, new LocalizedText("حد الثقة لتصحيح خطوات الرياضيات", "Math step grading: review threshold"), ThresholdDescription),
    ];
}
