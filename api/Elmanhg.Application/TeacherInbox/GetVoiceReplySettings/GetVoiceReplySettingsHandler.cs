using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using MediatR;

namespace Elmanhg.Application.TeacherInbox.GetVoiceReplySettings;

public sealed class GetVoiceReplySettingsHandler(IRuntimeSettings runtimeSettings) : IRequestHandler<GetVoiceReplySettingsQuery, VoiceReplySettingsResult>
{
    public async Task<VoiceReplySettingsResult> Handle(GetVoiceReplySettingsQuery request, CancellationToken cancellationToken)
    {
        var values = await runtimeSettings.GetValuesAsync(cancellationToken).ConfigureAwait(false);
        return new VoiceReplySettingsResult(values.Get(UploadRuntimeSettings.VoiceReplyMaxDurationSeconds), values.Get(UploadRuntimeSettings.VoiceReplyMaxSizeInMb));
    }
}
