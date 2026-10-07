using Core.DDD.Models;
using Core.Settings;
using Elmanhg.Application.Shared.Options;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Shared.RuntimeSettings.Definitions;

public sealed class UploadRuntimeSettings(IOptions<AskTeacherOptions> askTeacherOptions) : IRuntimeSettingDefinitions
{
    public const long BytesPerMegabyte = 1024L * 1024L;

    // Kestrel's MaxRequestBodySize is 10 MB; a 9 MB file plus multipart overhead stays under it.
    private const int MaxUploadMegabytes = 9;

    public static readonly RuntimeSettingKey<int> AskTeacherImageMaxSizeInMb = new("uploads.askTeacherImageMaxSizeInMb");
    public static readonly RuntimeSettingKey<int> VoiceReplyMaxSizeInMb = new("uploads.voiceReplyMaxSizeInMb");
    public static readonly RuntimeSettingKey<int> VoiceReplyMaxDurationSeconds = new("uploads.voiceReplyMaxDurationSeconds");

    public IReadOnlyList<RuntimeSettingDefinition> Definitions =>
    [
        RuntimeSettingDefinition.ForInteger(AskTeacherImageMaxSizeInMb, nameof(RuntimeSettingGroup.Uploads), askTeacherOptions.Value.ImageMaxSizeInMb, 1, MaxUploadMegabytes, new LocalizedText("أقصى حجم لصورة سؤال الطالب (ميجابايت)", "Student question photo: max size (MB)"), new LocalizedText("أكبر حجم لصورة يرفقها الطالب بسؤاله.", "The largest photo a student can attach to a question.")),
        RuntimeSettingDefinition.ForInteger(VoiceReplyMaxSizeInMb, nameof(RuntimeSettingGroup.Uploads), askTeacherOptions.Value.VoiceMaxSizeInMb, 1, MaxUploadMegabytes, new LocalizedText("أقصى حجم للرد الصوتي (ميجابايت)", "Voice reply: max size (MB)"), new LocalizedText("أكبر حجم لتسجيل صوتي يرسله المعلّم.", "The largest voice recording a teacher can send.")),
        RuntimeSettingDefinition.ForInteger(VoiceReplyMaxDurationSeconds, nameof(RuntimeSettingGroup.Uploads), askTeacherOptions.Value.VoiceMaxDurationSeconds, 10, 600, new LocalizedText("أقصى مدة للرد الصوتي (ثوانٍ)", "Voice reply: max length (seconds)"), new LocalizedText("أطول مدة لتسجيل صوتي يرسله المعلّم.", "The longest voice recording a teacher can send.")),
    ];
}
