using Elmanhg.Application.TeacherInbox.Shared;
using MediatR;

namespace Elmanhg.Application.TeacherInbox.ReplyToTeacherThread;

public sealed record ReplyToTeacherThreadCommand(Guid ThreadId, string? Text) : IRequest<TeacherInboxThreadResult>;
