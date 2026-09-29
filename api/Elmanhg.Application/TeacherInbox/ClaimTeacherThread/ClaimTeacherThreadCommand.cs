using Elmanhg.Application.TeacherInbox.Shared;
using MediatR;

namespace Elmanhg.Application.TeacherInbox.ClaimTeacherThread;

public sealed record ClaimTeacherThreadCommand(Guid ThreadId) : IRequest<TeacherInboxThreadResult>;
