using Elmanhg.Application.TeacherInbox.Shared;
using MediatR;

namespace Elmanhg.Application.TeacherInbox.GetInboxThread;

public sealed record GetInboxThreadQuery(Guid ThreadId) : IRequest<TeacherInboxThreadResult>;
