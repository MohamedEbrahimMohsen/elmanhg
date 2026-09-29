using Elmanhg.Application.TeacherInbox.Shared;
using MediatR;

namespace Elmanhg.Application.TeacherInbox.SendVoiceReply;

public sealed record SendVoiceReplyCommand(Guid ThreadId, Guid DraftId, string? Text) : IRequest<TeacherInboxThreadResult>;
