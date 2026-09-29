using Elmanhg.Application.TeacherThreads.Shared;
using MediatR;

namespace Elmanhg.Application.TeacherThreads.FollowUpTeacherThread;

public sealed record FollowUpTeacherThreadCommand(Guid ThreadId, string? Text) : IRequest<TeacherThreadResult>;
