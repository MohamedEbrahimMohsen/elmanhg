using Elmanhg.Application.TeacherThreads.Shared;
using MediatR;

namespace Elmanhg.Application.TeacherThreads.RateTeacherThread;

public sealed record RateTeacherThreadCommand(Guid ThreadId, int Rating) : IRequest<TeacherThreadResult>;
