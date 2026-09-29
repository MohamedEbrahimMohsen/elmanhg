using MediatR;

namespace Elmanhg.Application.TeacherThreads.ProcessTeacherThreadSla;

public sealed record ProcessTeacherThreadSlaCommand(Guid ThreadId) : IRequest;
