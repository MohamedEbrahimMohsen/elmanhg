using MediatR;

namespace Elmanhg.Application.TeacherInbox.GetVoiceReplySettings;

public sealed record GetVoiceReplySettingsQuery : IRequest<VoiceReplySettingsResult>;
