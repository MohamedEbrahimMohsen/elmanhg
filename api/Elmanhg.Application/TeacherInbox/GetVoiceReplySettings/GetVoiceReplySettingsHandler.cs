using Elmanhg.Application.Shared.Options;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TeacherInbox.GetVoiceReplySettings;

public sealed class GetVoiceReplySettingsHandler(IOptions<AskTeacherOptions> askTeacherOptions) : IRequestHandler<GetVoiceReplySettingsQuery, VoiceReplySettingsResult>
{
    public Task<VoiceReplySettingsResult> Handle(GetVoiceReplySettingsQuery request, CancellationToken cancellationToken)
    {
        var options = askTeacherOptions.Value;
        return Task.FromResult(new VoiceReplySettingsResult(options.VoiceMaxDurationSeconds, options.VoiceMaxSizeInMb));
    }
}
