using Core.DDD.Models;
using Elmanhg.Application.Shared.Messaging;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.TeacherThreads;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Shared.RuntimeSettings.Definitions;

public sealed class OutOfAppReminderRuntimeSettings(IOptions<AskTeacherOptions> askTeacherOptions) : IRuntimeSettingDefinitions
{
    public const string BothChannels = "Both";

    public static readonly RuntimeSettingKey<bool> Enabled = new("askTeacher.outOfAppReminderEnabled");
    public static readonly RuntimeSettingKey<string> Channels = new("askTeacher.outOfAppReminderChannels");
    public static readonly RuntimeSettingKey<string> Stage = new("askTeacher.outOfAppReminderStage");

    public static readonly IReadOnlyList<string> ChannelChoices = [nameof(MessageChannel.WhatsApp), nameof(MessageChannel.Email), BothChannels];
    public static readonly IReadOnlyList<string> StageChoices = [nameof(TeacherThreadSlaEventKind.FirstReminder), nameof(TeacherThreadSlaEventKind.SecondReminder)];

    public IReadOnlyList<RuntimeSettingDefinition> Definitions =>
    [
        RuntimeSettingDefinition.ForBoolean(Enabled, RuntimeSettingGroup.AskTeacher, askTeacherOptions.Value.OutOfAppReminderEnabled, new LocalizedText("تذكير المعلّم خارج التطبيق", "Out-of-app teacher reminder"), new LocalizedText("يرسل تذكيرًا واحدًا لكل سؤال عبر واتساب أو البريد الإلكتروني عند مرحلة التذكير المختارة.", "Sends one reminder per question by WhatsApp or email at the chosen reminder stage.")),
        RuntimeSettingDefinition.ForChoice(Channels, RuntimeSettingGroup.AskTeacher, askTeacherOptions.Value.OutOfAppReminderChannels, ChannelChoices, new LocalizedText("قنوات التذكير خارج التطبيق", "Out-of-app reminder channels"), new LocalizedText("واتساب يصل فقط للمعلّم المسجّل رقم هاتفه.", "WhatsApp reaches only teachers with a phone number on file.")),
        RuntimeSettingDefinition.ForChoice(Stage, RuntimeSettingGroup.AskTeacher, askTeacherOptions.Value.OutOfAppReminderStage, StageChoices, new LocalizedText("مرحلة التذكير خارج التطبيق", "Out-of-app reminder stage"), new LocalizedText("يُرسل التذكير خارج التطبيق مع هذا التذكير داخل التطبيق.", "The out-of-app reminder goes out with this in-app reminder.")),
    ];
}
