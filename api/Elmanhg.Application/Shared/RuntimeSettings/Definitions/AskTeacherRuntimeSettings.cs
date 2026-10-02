using Core.DDD.Models;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Shared.RuntimeSettings.Definitions;

public sealed class AskTeacherRuntimeSettings(IOptions<AskTeacherOptions> askTeacherOptions, IOptions<SubscriptionsOptions> subscriptionsOptions) : IRuntimeSettingDefinitions
{
    public static readonly RuntimeSettingKey<int> ReplySlaHours = new("askTeacher.replySlaHours");
    public static readonly RuntimeSettingKey<int> FirstReminderAfterHours = new("askTeacher.firstReminderAfterHours");
    public static readonly RuntimeSettingKey<int> SecondReminderAfterHours = new("askTeacher.secondReminderAfterHours");

    public IReadOnlyList<RuntimeSettingDefinition> Definitions =>
    [
        RuntimeSettingDefinition.ForInteger(ReplySlaHours, RuntimeSettingGroup.AskTeacher, subscriptionsOptions.Value.AskTeacherReplySlaHours, 1, 168, new LocalizedText("مهلة رد المعلّم (ساعات)", "Teacher reply time (hours)"), new LocalizedText("المدة التي يجب أن يرد فيها المعلّم على سؤال الطالب. تنطبق على الأسئلة والمتابعات الجديدة.", "How long a teacher has to reply to a student question. Applies to new questions and follow-ups.")),
        RuntimeSettingDefinition.ForInteger(FirstReminderAfterHours, RuntimeSettingGroup.AskTeacher, askTeacherOptions.Value.FirstReminderAfterHours, 1, 167, new LocalizedText("التذكير الأول بعد (ساعات)", "First reminder after (hours)"), new LocalizedText("عدد الساعات من بداية مهلة الرد قبل التذكير الأول. يجب أن يسبق التذكير الثاني.", "Hours into the reply window before the first reminder. Must come before the second reminder.")),
        RuntimeSettingDefinition.ForInteger(SecondReminderAfterHours, RuntimeSettingGroup.AskTeacher, askTeacherOptions.Value.SecondReminderAfterHours, 1, 167, new LocalizedText("التذكير الثاني بعد (ساعات)", "Second reminder after (hours)"), new LocalizedText("يجب أن يقع بين التذكير الأول ونهاية مهلة الرد.", "Must fall between the first reminder and the end of the reply window.")),
    ];

    public IReadOnlyList<RuntimeSettingConstraint> Constraints =>
    [
        new(ErrorCodes.AskTeacherReminderOrderInvalid, values => values.Get(FirstReminderAfterHours) < values.Get(SecondReminderAfterHours) && values.Get(SecondReminderAfterHours) < values.Get(ReplySlaHours)),
    ];
}
