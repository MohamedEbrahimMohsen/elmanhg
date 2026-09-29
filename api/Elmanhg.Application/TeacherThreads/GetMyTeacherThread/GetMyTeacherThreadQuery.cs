using Elmanhg.Application.TeacherThreads.Shared;
using MediatR;

namespace Elmanhg.Application.TeacherThreads.GetMyTeacherThread;

public sealed record GetMyTeacherThreadQuery(Guid ThreadId) : IRequest<TeacherThreadResult>;
